add_library(NavMesh STATIC)

target_sources(NavMesh PRIVATE
        src/RTNavMeshTerrain.cpp
)

target_include_directories(NavMesh
        PUBLIC
        include/
        "${DXSDK_DIR}/Include"
        PRIVATE include/NavMesh)

target_link_libraries(NavMesh "${DXSDK_DIR}/Include")
