add_library(GFX3DFunction STATIC)


target_sources(GFX3DFunction PRIVATE
        src/GFX3DFunction/DrawingHelpers.cpp
        src/GFX3DFunction/GFontTexture.cpp
        src/GFX3DFunction/GFXVideo3d.cpp
        src/GFX3DFunction/Common/d3dapp.cpp
        src/GFX3DFunction/Common/d3denumeration.cpp
        src/GFX3DFunction/Common/d3dsettings.cpp
        src/GFX3DFunction/Common/d3dutil.cpp
        src/GFX3DFunction/Common/dxutil.cpp
        src/GFX3DFunction/RStateMgr.cpp
        src/GFX3DFunction/RTLoading.cpp
)

target_compile_options(GFX3DFunction PRIVATE
    /wd4244                          # narrowing (MSVC style, ignored by clang-cl)
    -Wno-c++11-narrowing             # clang: suppress narrowing in switch cases
    -D_CRT_SECURE_NO_WARNINGS        # silence strcpy/strcat deprecation spam
)

target_include_directories(GFX3DFunction PUBLIC
    src/
    "${DXSDK_DIR}/Include"
    "${CMAKE_SOURCE_DIR}/src"
)

target_link_libraries(GFX3DFunction PRIVATE
    GFXMainFrame
    GFXFileManagerLIB
    d3d9
)