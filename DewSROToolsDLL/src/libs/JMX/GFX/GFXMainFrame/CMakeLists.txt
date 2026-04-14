add_library(GFXMainFrame STATIC)

target_sources(GFXMainFrame PRIVATE
    src/GFXMainFrame/GFXMainFrame.cpp
    src/GFXMainFrame/GWnd.cpp
    src/GFXMainFrame/GWndBase.cpp
    src/GFXMainFrame/Process.cpp
    src/GFXMainFrame/Camera.cpp
    src/GFXMainFrame/Controler.cpp
)

target_include_directories(GFXMainFrame PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    "${DXSDK_DIR}/Include"
)

target_link_libraries(GFXMainFrame PRIVATE
    BSLib
    d3d9
)