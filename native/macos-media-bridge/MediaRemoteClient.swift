import Foundation

final class MediaRemoteClient {
    static let shared = MediaRemoteClient()

    private typealias GetNowPlayingInfoFn = @convention(c) (
        DispatchQueue, @escaping ([String: Any]?) -> Void
    ) -> Void
    private typealias SendCommandFn = @convention(c) (Int32, CFDictionary?) -> Void
    private typealias RegisterNotificationsFn = @convention(c) (DispatchQueue) -> Void

    private let getNowPlayingInfo: GetNowPlayingInfoFn?
    private let sendCommand: SendCommandFn?
    private let registerNotifications: RegisterNotificationsFn?

    private var cachedInfo: [String: Any]?
    private let infoLock = NSLock()

    private init() {
        let bundlePath = "/System/Library/PrivateFrameworks/MediaRemote.framework"
        guard let bundle = CFBundleCreate(kCFAllocatorDefault, NSURL(fileURLWithPath: bundlePath)) else {
            getNowPlayingInfo = nil
            sendCommand = nil
            registerNotifications = nil
            return
        }

        getNowPlayingInfo = Self.load(bundle, "MRMediaRemoteGetNowPlayingInfo", GetNowPlayingInfoFn.self)
        sendCommand = Self.load(bundle, "MRMediaRemoteSendCommand", SendCommandFn.self)
        registerNotifications = Self.load(
            bundle, "MRMediaRemoteRegisterForNowPlayingNotifications", RegisterNotificationsFn.self
        )
    }

    var isAvailable: Bool { getNowPlayingInfo != nil }

    func registerForNotifications(on queue: DispatchQueue) {
        registerNotifications?(queue)
    }

    func fetchNowPlayingInfo(completion: @escaping ([String: Any]?) -> Void) {
        guard let getNowPlayingInfo else {
            completion(nil)
            return
        }

        getNowPlayingInfo(DispatchQueue.main) { [weak self] info in
            let sanitized = Self.sanitize(info)
            self?.infoLock.lock()
            self?.cachedInfo = sanitized
            self?.infoLock.unlock()
            completion(sanitized)
        }
    }

    func cachedNowPlayingInfo() -> [String: Any]? {
        infoLock.lock()
        defer { infoLock.unlock() }
        return cachedInfo
    }

    func send(_ command: Int32, options: CFDictionary? = nil) {
        sendCommand?(command, options)
    }

    private static func load<T>(_ bundle: CFBundle, _ name: String, _ type: T.Type) -> T? {
        guard let pointer = CFBundleGetFunctionPointerForName(bundle, name as CFString) else {
            return nil
        }
        return unsafeBitCast(pointer, to: type)
    }

    private static func sanitize(_ info: [String: Any]?) -> [String: Any]? {
        guard var info else { return nil }
        // Accessing artwork data directly can crash on newer macOS versions.
        if info[MRKeys.artworkData] != nil {
            info[MRKeys.artworkData] = Data()
        }
        return info
    }
}
