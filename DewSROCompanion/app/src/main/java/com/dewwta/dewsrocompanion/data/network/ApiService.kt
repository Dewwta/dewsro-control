package com.dewwta.dewsrocompanion.data.network

import com.dewwta.dewsrocompanion.data.model.CharacterWithState
import com.dewwta.dewsrocompanion.data.model.LoginRequest
import com.dewwta.dewsrocompanion.data.model.LoginResponse
import com.dewwta.dewsrocompanion.data.model.MessageResponse
import com.dewwta.dewsrocompanion.data.model.SilkResponse
import com.dewwta.dewsrocompanion.data.model.UpdateProfileRequest
import com.dewwta.dewsrocompanion.data.model.UserDto
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT

interface ApiService {

    @POST("api/account/login")
    suspend fun login(@Body request: LoginRequest): Response<LoginResponse>

    @GET("api/account/me")
    suspend fun getMe(): Response<UserDto>

    @GET("api/account/silk")
    suspend fun getSilk(): Response<SilkResponse>

    @PUT("api/account/profile")
    suspend fun updateProfile(@Body request: UpdateProfileRequest): Response<MessageResponse>

    @GET("api/characters")
    suspend fun getMyCharacters(): Response<List<CharacterWithState>>
}
