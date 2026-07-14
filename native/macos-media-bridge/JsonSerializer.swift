import Foundation
import AppKit

enum JsonSerializer {
    static let maxCoverSize: CGFloat = 600
    static let jpegQuality: CGFloat = 0.75
    static let iconSize: CGFloat = 64

    static func emit(_ state: BridgeState) {
        let encoder = JSONEncoder()
        guard let data = try? encoder.encode(state),
              let json = String(data: data, encoding: .utf8) else { return }
        print("DATA:\(json)")
        fflush(stdout)
    }

    static func encodeCoverJpeg(_ rawData: Data) -> String {
        guard let image = NSImage(data: rawData) else { return "" }
        guard let resized = resize(image, to: maxCoverSize) else { return "" }
        guard let tiff = resized.tiffRepresentation,
              let rep = NSBitmapImageRep(data: tiff) else { return "" }
        let props: [NSBitmapImageRep.PropertyKey: Any] = [.compressionFactor: jpegQuality]
        guard let jpeg = rep.representation(using: .jpeg, properties: props) else { return "" }
        return jpeg.base64EncodedString()
    }

    static func encodeAppIcon(bundleId: String) -> String {
        guard !bundleId.isEmpty,
              let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleId) else {
            return ""
        }
        let icon = NSWorkspace.shared.icon(forFile: url.path)
        icon.size = NSSize(width: iconSize, height: iconSize)
        guard let tiff = icon.tiffRepresentation,
              let rep = NSBitmapImageRep(data: tiff) else { return "" }
        guard let png = rep.representation(using: .png, properties: [:]) else { return "" }
        return png.base64EncodedString()
    }

    private static func resize(_ image: NSImage, to maxSize: CGFloat) -> NSImage? {
        let size = image.size
        let scale = min(maxSize / size.width, maxSize / size.height, 1.0)
        let newSize = NSSize(width: size.width * scale, height: size.height * scale)
        let newImage = NSImage(size: newSize)
        newImage.lockFocus()
        image.draw(in: NSRect(origin: .zero, size: newSize),
                   from: NSRect(origin: .zero, size: size),
                   operation: .copy, fraction: 1.0)
        newImage.unlockFocus()
        return newImage
    }
}
