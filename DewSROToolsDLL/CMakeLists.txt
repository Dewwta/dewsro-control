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

add_subdirectory(src/BSLib) 

add_library(DewSROToolkit SHARED
    src/DllMain.cpp
    "src/Control.cpp"
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
    "src/net/LoginHook.h"
    "src/Settings.h" 
    "src/net/DllBridge.cpp" "src/client/ClassLink.h" "src/client/TypeId/TypeIdBase.h" "src/client/TypeId/TypeIdRegistry.cpp" "src/client/TypeId/TypeId.h" "src/client/data/CommonData.h" "src/Logging/Logger.h" "src/Logging/Logger.cpp")

target_include_directories(DewSROToolkit PRIVATE
    src/
    kiero/
    imgui/
    imgui/backends/
    minhook/include/
    BSLib

    "${DXSDK_DIR}/Include"
)
set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /NODEFAULTLIB:d3dx9dt.lib /DEFAULTLIB:d3dx9d.lib")
target_link_libraries(DewSROToolkit PRIVATE
    d3d9.lib
    d3dx9.lib
    dwmapi.lib
    minhook
    BSLib
    ws2_32
)

target_link_options(DewSROToolkit PRIVATE /DEF:${CMAKE_SOURCE_DIR}/src/exports.def)

set_target_properties(DewSROToolkit PROPERTIES
    OUTPUT_NAME "DewSROToolkit"
)
