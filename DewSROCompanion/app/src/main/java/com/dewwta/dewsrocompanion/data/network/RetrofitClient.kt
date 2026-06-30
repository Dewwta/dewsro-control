package com.dewwta.dewsrocompanion.data.network

import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.security.SecureRandom
import java.security.cert.X509Certificate
import java.util.concurrent.TimeUnit
import javax.net.ssl.SSLContext
import javax.net.ssl.X509TrustManager

object RetrofitClient {

    const val DEFAULT_BASE_URL = "https://192.168.0.11:5086/"

    private var _token: String? = null
    private var _baseUrl: String = DEFAULT_BASE_URL

    // Lazily built; reset to null whenever URL changes so it gets rebuilt
    private var _service: ApiService? = null

    fun setToken(token: String?) {
        _token = token
    }

    /** Call this after loading the stored URL from DataStore, or when the user saves a new one. */
    fun setBaseUrl(url: String) {
        val normalised = url.trimEnd('/') + "/"
        if (normalised != _baseUrl) {
            _baseUrl = normalised
            _service = null   // force rebuild on next access
        }
    }

    fun currentBaseUrl(): String = _baseUrl

    private val authInterceptor = Interceptor { chain ->
        val req = chain.request().newBuilder().apply {
            _token?.let { addHeader("Authorization", "Bearer $it") }
        }.build()
        chain.proceed(req)
    }

    private val logging = HttpLoggingInterceptor().apply {
        level = HttpLoggingInterceptor.Level.BODY
    }

    // Trust-all SSL for dev / self-signed certs
    private val trustAll = object : X509TrustManager {
        override fun checkClientTrusted(chain: Array<X509Certificate>?, authType: String?) {}
        override fun checkServerTrusted(chain: Array<X509Certificate>?, authType: String?) {}
        override fun getAcceptedIssuers(): Array<X509Certificate> = arrayOf()
    }

    private val httpClient: OkHttpClient by lazy {
        val sslCtx = SSLContext.getInstance("TLS").apply {
            init(null, arrayOf(trustAll), SecureRandom())
        }
        OkHttpClient.Builder()
            .sslSocketFactory(sslCtx.socketFactory, trustAll)
            .hostnameVerifier { _, _ -> true }
            .addInterceptor(authInterceptor)
            .addInterceptor(logging)
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(15, TimeUnit.SECONDS)
            .build()
    }

    val service: ApiService
        get() {
            return _service ?: Retrofit.Builder()
                .baseUrl(_baseUrl)
                .client(httpClient)
                .addConverterFactory(GsonConverterFactory.create())
                .build()
                .create(ApiService::class.java)
                .also { _service = it }
        }
}
