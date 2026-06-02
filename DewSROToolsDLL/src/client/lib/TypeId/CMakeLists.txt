add_library(TypeId STATIC)

target_sources(TypeId PRIVATE
    src/TypeIdRegistry.cpp
)

target_include_directories(TypeId PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_SOURCE_DIR}/src
    ${CMAKE_SOURCE_DIR}/src/BSLib
)

# (We can ignore the cxxtest part — you don't need it)