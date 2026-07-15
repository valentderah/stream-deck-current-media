SWIFT_FILES = main.swift MediaRemoteClient.swift MediaRemoteBridge.swift JsonSerializer.swift
FRAMEWORKS = -framework Foundation -framework AppKit

media-bridge: $(SWIFT_FILES) MediaRemote.h
	swiftc -O $(SWIFT_FILES) \
	  -import-objc-header MediaRemote.h \
	  -target x86_64-apple-macos10.15 -target arm64-apple-macos11.0 \
	  $(FRAMEWORKS) -o media-bridge
	@echo "Built: $$(lipo -info media-bridge)"

clean:
	rm -f media-bridge

.PHONY: clean
