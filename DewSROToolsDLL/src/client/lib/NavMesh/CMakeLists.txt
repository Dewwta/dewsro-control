add_library(NavMesh STATIC)

target_sources(NavMesh PRIVATE
    src/RTNavMeshTerrain.cpp
    # Add any other .cpp files from the src/ folder here if they exist
)

target_include_directories(NavMesh PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/include/NavMesh     # ← THIS FIXES THE ERROR
    ${CMAKE_SOURCE_DIR}/src
    ${CMAKE_SOURCE_DIR}/src/BSLib
    "${DXSDK_DIR}/Include"
)

target_link_libraries(NavMesh PRIVATE
    d3d9
    BSLib
)