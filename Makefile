.PHONY: build-windows pack zip prod

create-builds-dir:
	@if not exist builds mkdir builds

build-windows:
	@echo "Building for Windows..."
	cd MediaManager/platforms/windows && dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false
	@echo "Copying executable..."
	xcopy /Y MediaManager\\platforms\\windows\\bin\\Release\\net8.0-windows10.0.17763.0\\win-x64\\publish\\CurrentMedia.exe ru.valentderah.current-media.sdPlugin\\

pack: create-builds-dir
	@echo "Packing plugin with Stream Deck CLI..."
	streamdeck pack ru.valentderah.current-media.sdPlugin -o builds

zip: create-builds-dir
	@echo "Zipping plugin directory..."
	@powershell -Command "Compress-Archive -Path ./ru.valentderah.current-media.sdPlugin/* -DestinationPath builds/current-media.sdPlugin.zip -Force"
	@echo "Plugin zipped into builds/current-media.sdPlugin.zip"

prod: build-windows zip pack
	@echo "Production build complete!"
