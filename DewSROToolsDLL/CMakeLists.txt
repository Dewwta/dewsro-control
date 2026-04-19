cmake_minimum_required(VERSION 3.20)
project(DewSROToolkit LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(DXSDK_DIR "C:/Program Files (x86)/Microsoft DirectX SDK (June 2010)" CACHE PATH "DirectX SDK path")

# -------------------------
# DirectX SDK
# -------------------------
link_directories("${DXSDK_DIR}/Lib/x86")

# -------------------------
# Force x86 for MSVC properly
# -------------------------
if (MSVC)
    add_compile_options(/arch:SSE2)
    set(CMAKE_GENERATOR_PLATFORM Win32)
endif()

# -------------------------
# Dependencies
# -------------------------
add_subdirectory(minhook)
add_subdirectory(src/BSLib)

# -------------------------
# Source files
# -------------------------
set(SOURCES
    src/DllMain.cpp
    src/Control.cpp

    # Hooks
    src/hooks/dx9_hook.cpp
    src/hooks/patcher.cpp
    src/hooks/patcher.h

    # Net
    src/net/DllBridge.cpp
    src/net/NetActions.h
    src/net/LoginHook.h

    # Client
    src/client/ClassLink.h
    src/client/data/CommonData.h
    src/client/TypeId/TypeId.h
    src/client/TypeId/TypeIdBase.h
    src/client/TypeId/TypeIdRegistry.cpp

    # Memory
    src/mem/Process.h
    src/mem/hook.h

    # Logging
    src/Logging/Logger.cpp
    src/Logging/Logger.h

    # Settings
    src/Settings.h

    # ImGui
    imgui/imgui.cpp
    imgui/imgui_draw.cpp
    imgui/imgui_tables.cpp
    imgui/imgui_widgets.cpp
    imgui/imgui_demo.cpp
    imgui/backends/imgui_impl_dx9.cpp
    imgui/backends/imgui_impl_win32.cpp

    # Kiero
    kiero/kiero.cpp
    "src/client/RewardWindow.cpp")

add_library(DewSROToolkit SHARED ${SOURCES} "src/client/RewardWindow.h" "src/client/RewardWindow.cpp" "src/client/SoxOverlay.h")

# -------------------------
# Includes
# -------------------------
target_include_directories(DewSROToolkit PRIVATE
    src/
    kiero/
    imgui/
    imgui/backends/
    minhook/include/
    BSLib/
    "${DXSDK_DIR}/Include"
)

# -------------------------
# Link libs
# -------------------------
target_link_libraries(DewSROToolkit PRIVATE
    d3d9.lib
    d3dx9.lib
    dwmapi.lib
    minhook
    BSLib
    ws2_32
)

# -------------------------
# Linker options
# -------------------------
target_link_options(DewSROToolkit PRIVATE
    /DEF:${CMAKE_SOURCE_DIR}/src/exports.def
    /MACHINE:X86
)

# -------------------------
# Output name
# -------------------------
set_target_properties(DewSROToolkit PROPERTIES
    OUTPUT_NAME "DewSROToolkit"
)