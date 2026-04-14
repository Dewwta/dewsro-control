
add_library(TypeId STATIC)

target_sources(TypeId PRIVATE
        src/TypeIdRegistry.cpp
)

target_include_directories(TypeId PUBLIC include/)

if (CXXTEST_FOUND)
    cxxtest_add_test(TestTypeId GeneratedTestTypeId.cpp ${CMAKE_CURRENT_SOURCE_DIR}/test/TestTypeId.hpp)
    target_link_libraries(TestTypeId TypeId)
endif ()
