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
    src/
    ${CMAKE_SOURCE_DIR}/src                 # for "BSLib/..."
    ${CMAKE_SOURCE_DIR}/src/BSLib           # for "support/..."
    "${DXSDK_DIR}/Include"
)

target_link_libraries(GFXMainFrame PRIVATE
    BSLib
    d3d9
    # removed "support" — it was never a target
)