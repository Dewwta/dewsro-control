add_library(BSLib STATIC)

target_sources(BSLib PRIVATE
    BSLib.cpp
    MsgHandler.cpp
    multibyte.cpp
    StringCheck.cpp
    Debug.cpp
    _internal/MemPoolManager.cpp
    ClientNet/MsgStreamBuffer.cpp
 "ClientNet/ClientNet.cpp" "ClientNet/ClientNet.h" "ClientNet/IClientNet.h")

target_compile_definitions(BSLib PRIVATE _SILENCE_STDEXT_HASH_DEPRECATION_WARNINGS)

target_include_directories(BSLib PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/..)