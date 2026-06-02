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
    ${CMAKE_SOURCE_DIR}/src
    ${CMAKE_SOURCE_DIR}/src/BSLib
    ${CMAKE_SOURCE_DIR}/src/client/lib/Client/src     # ← THIS FIXES Game.h
    ${CMAKE_SOURCE_DIR}/src/client/lib/SimpleViewer/include/     # ← THIS FIXES Game.h

)

target_link_libraries(SROInterfaceLIB PRIVATE
    BSLib
    FM
    GFXMainFrame
    GFX3DFunction
    ClientLib      # ← add this too
)