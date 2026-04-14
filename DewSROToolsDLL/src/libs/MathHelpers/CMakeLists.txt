add_library(MathHelpers STATIC)
target_sources(MathHelpers PRIVATE
        src/MathHelpers/hsl.cpp
)

target_include_directories(MathHelpers
        PUBLIC
        src/
)

target_link_libraries(MathHelpers d3d9 d3dx9)

