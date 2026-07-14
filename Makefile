.PHONY: build-windows build-macos pack prod

build-windows:
	scripts\build-windows.bat

build-macos:
	@echo Run on macOS only:
	bash scripts/build-macos.sh

pack:
	streamdeck pack ru.valentderah.current-media.sdPlugin -o dist --force

prod-windows: build-windows pack
