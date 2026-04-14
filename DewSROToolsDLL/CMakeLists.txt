cmake_minimum_required(VERSION 3.20)
project(DewSROToolkit)
set(DXSDK_DIR "C:/Program Files (x86)/Microsoft DirectX SDK (June 2010)" CACHE PATH "DirectX SDK path")
link_directories("${DXSDK_DIR}/Lib/x86")
set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -m32")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -m32")
set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -machine:x86")
set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -machine:x86")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wno-invalid-offsetof")
if(MSVC OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
    # -w suppresses ALL warnings
    # -Wno-invalid-offsetof
    add_compile_options(-w) 
endif()

add_subdirectory(minhook)

# === JMX / GFX Libraries ===
add_subdirectory(src/BSLib) 
add_subdirectory(src/libs/JMX/GFX/FM)               # GFXFileManagerLIB
add_subdirectory(src/libs/JMX/GFX/GFXMainFrame)     # GFXMainFrame
add_subdirectory(src/libs/JMX/GFX/GFX3DFunction)    # GFX3DFunction
add_subdirectory(src/libs/NavMesh)                  # NavMesh
add_subdirectory(src/libs/JMX/SROInterfaceLib)      # SROInterfaceLIB
add_subdirectory(src/libs/SimpleViewer)             # SimpleViewer
add_subdirectory(src/libs/TypeId)                   # TypeId
add_subdirectory(src/libs/ClientLib)                # support

add_library(DewSROToolkit SHARED
    src/DllMain.cpp
    src/AppManager.cpp
    src/hooks/dx9_hook.cpp
    kiero/kiero.cpp
    imgui/imgui.cpp
    imgui/imgui_draw.cpp
    imgui/imgui_tables.cpp
    imgui/imgui_widgets.cpp
    imgui/backends/imgui_impl_dx9.cpp
    imgui/backends/imgui_impl_win32.cpp
    imgui/imgui_demo.cpp
    "src/net/NetActions.h"
    "src/Settings.h" 
    "src/Interfaces/PlayerInterface.h"
    "src/game/System.cpp"
 "src/Interfaces/PlayerInterface.cpp" "src/net/DllBridge.cpp")

target_include_directories(DewSROToolkit PRIVATE
    src/
    kiero/
    imgui/
    imgui/backends/
    minhook/include/
    BSLib

    # FIXED ROOTS (this is the important part)
    src/libs/TypeId
    src/libs/JMX/GFX
    src/libs/JMX/GFX/GFXMainFrame
    src/libs/JMX/GFX/GFX3DFunction
    src/libs/JMX/GFX/FM
    src/libs/ClientLib/src
    src/libs/NavMesh
    src/libs/SimpleViewer/include

    "${DXSDK_DIR}/Include"
)
set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /NODEFAULTLIB:d3dx9dt.lib /DEFAULTLIB:d3dx9d.lib")
target_link_libraries(DewSROToolkit PRIVATE
    d3d9.lib
    d3dx9.lib        # Often required by GFX libraries
    d3dxof.lib       # Fixed the error
    dwmapi.lib
    minhook
    BSLib
    GFXFileManagerLIB
    GFXMainFrame
    GFX3DFunction
    NavMesh
    TypeId
    SROInterfaceLIB
    ClientLib
    SimpleViewer
    ws2_32
)

target_link_options(DewSROToolkit PRIVATE /DEF:${CMAKE_SOURCE_DIR}/src/exports.def)

set_target_properties(DewSROToolkit PROPERTIES
    OUTPUT_NAME "DewSROToolkit"
)
