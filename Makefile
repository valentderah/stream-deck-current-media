PLUGIN_NAME = ru.valentderah.current-media.sdPlugin
DIST_DIR = dist/$(PLUGIN_NAME)

.PHONY: build static build-static build-windows build-macos pack prod prod-windows

static:
ifeq ($(OS),Windows_NT)
	scripts\sync-static.bat 
else
	bash scripts/sync-static.sh
endif

pack:
	@echo "==> Packaging plugin..."
	streamdeck pack "$(DIST_DIR)" -o dist --force

build-windows: static
ifeq ($(OS),Windows_NT)
	scripts\build-windows.bat
else
	bash scripts/build-windows.sh
endif

build-macos: static
	bash scripts/build-macos.sh

build:
ifeq ($(OS),Windows_NT)
	$(MAKE) build-windows
else
	$(MAKE) build-macos build-windows
endif

prod: build pack
