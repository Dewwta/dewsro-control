add_library(FM STATIC)

target_sources(FM PRIVATE
    src/IFileManager.cpp
    # ← add any other .cpp files from the original GFXFileManager here
)

target_include_directories(FM
    PUBLIC
        include/
        ${CMAKE_SOURCE_DIR}/src/BSLib          # for support/undefined.h
    PRIVATE
        include/GFXFM
)

target_link_libraries(FM PRIVATE BSLib)