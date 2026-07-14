PLUGIN_NAME = ru.valentderah.current-media.sdPlugin
ASSETS_DIR = assets
DIST_DIR = dist/$(PLUGIN_NAME)

ifeq ($(OS),Windows_NT)
DIST_WIN = dist/$(PLUGIN_NAME)

SYNC_CMD = cmd /c "if not exist $(DIST_WIN) mkdir $(DIST_WIN) & robocopy $(ASSETS_DIR) $(DIST_WIN) /MIR /XD win mac /NFL /NDL /NJH /NJS & if %%ERRORLEVEL%% LEQ 7 (exit /b 0) else (exit /b 1)"
BUILD_CMD = scripts\build-windows.bat
else
SYNC_CMD = mkdir -p "$(DIST_DIR)" && rsync -a --delete --exclude 'win/' --exclude 'mac/' "$(ASSETS_DIR)/" "$(DIST_DIR)/"
BUILD_CMD = bash scripts/build-macos.sh
endif

PACK_CMD = streamdeck pack "$(DIST_DIR)" -o dist --force

.PHONY: build static build-static build-windows build-macos pack prod prod-windows

build:
ifeq ($(OS),Windows_NT)
	$(MAKE) build-windows
else
	$(MAKE) build-macos
endif

static:
	@echo "==> Syncing static assets..."
	@$(SYNC_CMD)
	@echo "    Done."


build-windows: static
	$(BUILD_CMD)

build-macos: static
	$(BUILD_CMD)

pack:
	@echo "==> Packaging plugin..."
	$(PACK_CMD)

prod: build pack
