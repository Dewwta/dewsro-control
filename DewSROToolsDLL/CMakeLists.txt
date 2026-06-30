cmake_minimum_required(VERSION 3.20)
project(DewSROToolkit LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(DXSDK_DIR "C:/Program Files (x86)/Microsoft DirectX SDK (June 2010)" CACHE PATH "DirectX SDK path")


# DirectX SDK
link_directories("${DXSDK_DIR}/Lib/x86")


# Force x86 for MSVC
if (MSVC)
    add_compile_options(/arch:SSE2)
    set(CMAKE_GENERATOR_PLATFORM Win32)

    # The reverse-engineered struct fixtures (ENSURE_SIZE/ENSURE_OFFSET) require
    # STL containers to carry the iterator-debugging proxy pointer, matching the
    # original client's VS2005-era layout. Release defaults to IDL=0 which strips
    # that pointer and shrinks every container by 4 bytes, breaking the ABI.
    add_compile_definitions($<$<NOT:$<CONFIG:Debug>>:_ITERATOR_DEBUG_LEVEL=1>)

    # Static CRT so the DLL runs on machines without the VC++ redistributable.
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

    # Toolsets newer than 14.38 (VS2022 v143) dropped Windows 7 support: their
    # STL/CRT import CopyFile2, CreateFile2, GetSystemTimePreciseAsFileTime etc.
    # directly, so the DLL fails to load on Win7. Build with -vcvars_ver=14.38
    # (see build-release.cmd) or the presets in CMakePresets.json.
    if (MSVC_VERSION GREATER 1938 AND NOT CMAKE_BUILD_TYPE STREQUAL "Debug")
        message(FATAL_ERROR
            "MSVC toolset ${MSVC_VERSION} > 14.38: the built DLL will NOT load on Windows 7. "
            "Build with build-release.cmd, or delete the cache and reconfigure with the "
            "x86-Release preset / vcvars32.bat -vcvars_ver=14.38.")
    endif()
endif()

# Dependencies
add_subdirectory(minhook)
add_subdirectory(src/BSLib)
add_subdirectory(src/client/lib/GFX/FM)
add_subdirectory(src/client/lib/GFX/GFXMainFrame)
add_subdirectory(src/client/lib/GFX/GFX3DFunction)
add_subdirectory(src/client/lib/InterfaceLib)
add_subdirectory(src/client/lib/NavMesh)
add_subdirectory(src/client/lib/SimpleViewer)
add_subdirectory(src/client/lib/TypeId)
add_subdirectory(src/client/lib/Client)

# Source
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
    src/client/RewardWindow.cpp
    src/client/AchievementWindow.cpp
    src/client/SROSkinWindow.cpp
    "src/client/pk2/Pk2Reader.cpp" "src/client/pk2/Blowfish.cpp" "src/client/pk2/IconCache.cpp")

add_library(DewSROToolkit SHARED ${SOURCES} "src/client/RewardWindow.h" "src/client/RewardWindow.cpp" "src/client/SoxOverlay.h" "src/client/AchievementWindow.h" "src/client/AchievementWindow.cpp" "src/client/stb_image.h" "src/client/SROSkinWindow.h" "src/client/SROSkinWindow.cpp" "src/client/pk2/Pk2Reader.h" "src/client/pk2/Pk2Reader.cpp" "src/client/pk2/Blowfish.h" "src/client/pk2/Blowfish.cpp" "src/client/pk2/IconCache.h" "src/client/pk2/IconCache.cpp")

# Includes
target_include_directories(DewSROToolkit PRIVATE
    src/
    kiero/
    imgui/
    imgui/backends/
    minhook/include/
    BSLib/
    src/client/lib/GFX/FM/include/
    src/client/lib/GFX/GFXMainFrame/include/
    src/client/lib/GFX/GFX3DFunction/include/
    src/client/lib/InterfaceLib/include/
    src/client/lib/NavMesh/include/NavMesh
    src/client/lib/SimpleViewer/include/SimpleViewer
    src/client/lib/TypeId/include/
    src/client/lib/Client/
    src/client/lib/CharDependentData/
    src/client/lib/Data/
    "${DXSDK_DIR}/Include"
)


# Link libs
target_link_libraries(DewSROToolkit PRIVATE
    d3d9.lib
    d3dx9.lib
    dwmapi.lib
    minhook
    BSLib
    ws2_32
    GFXMainFrame
    GFX3DFunction
    FM
    SROInterfaceLIB
    NavMesh
    SimpleViewer
    TypeId
    ClientLib
)


# Linker options
target_link_options(DewSROToolkit PRIVATE
    /DEF:${CMAKE_SOURCE_DIR}/src/exports.def
    /MACHINE:X86
)


# Output name
set_target_properties(DewSROToolkit PROPERTIES
    OUTPUT_NAME "DewSROToolkit"
)