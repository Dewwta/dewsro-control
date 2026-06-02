add_library(SimpleViewer INTERFACE)

target_include_directories(SimpleViewer INTERFACE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/include/SimpleViewer     # ← needed for "SimpleViewer/..."
)