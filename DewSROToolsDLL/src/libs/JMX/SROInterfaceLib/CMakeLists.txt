add_library(SROInterfaceLIB STATIC)

target_sources(SROInterfaceLIB PRIVATE
    src/SRIFLib/NInterfaceResource.cpp
    src/SRIFLib/NGFontTexture.cpp
    src/SRIFLib/NIFTextBoard.cpp
    src/SRIFLib/NIFWnd.cpp
    src/SRIFLib/NIFUnderMenuBar.cpp
    src/SRIFLib/NIFCommunityWnd.cpp
    src/SRIFLib/NIFBlockWnd.cpp
    src/SRIFLib/NIFTileWnd.cpp
    src/SRIFLib/NIFFrame.cpp
    src/SRIFLib/NIFMainFrame.cpp
)

target_include_directories(SROInterfaceLIB PUBLIC
    src/
    ../../ClientLib/src          # ← correct depth
    ../../SimpleViewer/include   # ← correct depth
    ../../TypeId/include
)
target_link_libraries(SROInterfaceLIB PRIVATE
    BSLib
    GFXFileManagerLIB
    GFX3DFunction
    GFXMainFrame
    #support
    # DO NOT link ClientLib here — it would create a target-not-found error
)