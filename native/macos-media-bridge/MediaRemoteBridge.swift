import Foundation
import AppKit

final class MediaRemoteBridge {
    static let shared = MediaRemoteBridge()

    private var lastElapsedTime: Double = 0
    private var lastArtworkData: Data?
    private var lastCoverBase64: String = ""

    func start() {
        let queue = DispatchQueue.main
        MRMediaRemoteRegisterForNowPlayingNotifications(queue)

        let center = DistributedNotificationCenter.default()
        center.addObserver(forName: NSNotification.Name(
            kMRMediaRemoteNowPlayingInfoDidChangeNotification as String), object: nil, queue: .main) { _ in
            self.emitCurrentState()
        }
        center.addObserver(forName: NSNotification.Name(
            kMRMediaRemotePlaybackStateDidChangeNotification as String), object: nil, queue: .main) { _ in
            self.emitCurrentState()
        }
    }

    func emitCurrentState() {
        let state = buildState()
        JsonSerializer.emit(state)
    }

    func getElapsedTime() -> Double {
        guard let info = MRMediaRemoteGetNowPlayingInfo(DispatchQueue.main) as? [String: Any] else {
            return lastElapsedTime
        }
        if let elapsed = info[kMRMediaRemoteNowPlayingInfoElapsedTime as String] as? Double {
            lastElapsedTime = elapsed
        }
        return lastElapsedTime
    }

    func handleCommand(_ line: String) {
        guard line.hasPrefix("cmd:") else { return }
        let cmd = String(line.dropFirst(4))

        switch cmd {
        case "play_pause":
            sendCommand(kMRMediaRemoteCommandTogglePlayPause, options: nil)
        case "next":
            sendCommand(kMRMediaRemoteCommandNextTrack, options: nil)
        case "previous":
            sendCommand(kMRMediaRemoteCommandPreviousTrack, options: nil)
        case "seek_forward":
            seek(by: 10.0)
        case "seek_backward":
            seek(by: -10.0)
        default:
            break
        }
    }

    private func seek(by offset: Double) {
        let newPos = max(0, getElapsedTime() + offset)
        let options = [kMRMediaRemoteOptionPlaybackPosition as String: newPos] as CFDictionary
        sendCommand(kMRMediaRemoteCommandChangePlaybackPosition, options: options)
    }

    private func sendCommand(_ command: Int, options: CFDictionary?) {
        MRMediaRemoteSendCommand(command, options)
    }

    private func buildState() -> BridgeState {
        guard let info = MRMediaRemoteGetNowPlayingInfo(DispatchQueue.main) as? [String: Any] else {
            return BridgeState.inactive()
        }

        let title = info[kMRMediaRemoteNowPlayingInfoTitle as String] as? String ?? ""
        let artist = info[kMRMediaRemoteNowPlayingInfoArtist as String] as? String ?? ""
        let album = info[kMRMediaRemoteNowPlayingInfoAlbum as String] as? String ?? ""
        let bundleId = info[kMRMediaRemoteNowPlayingApplicationBundleIdentifier as String] as? String ?? ""
        let elapsed = info[kMRMediaRemoteNowPlayingInfoElapsedTime as String] as? Double ?? 0
        let duration = info[kMRMediaRemoteNowPlayingInfoDuration as String] as? Double ?? 0
        let rate = info[kMRMediaRemoteNowPlayingInfoPlaybackRate as String] as? Double ?? 0

        let hasTrackData = !title.isEmpty || !artist.isEmpty
        let isRunning = bundleId.isEmpty ? false :
            NSWorkspace.shared.runningApplications.contains { $0.bundleIdentifier == bundleId }
        let isActive = (hasTrackData && isRunning) || (hasTrackData && rate > 0)

        let playbackState: String
        if rate > 0 { playbackState = "playing" }
        else if hasTrackData { playbackState = "paused" }
        else { playbackState = "stopped" }

        var coverBase64 = ""
        if let artworkData = info[kMRMediaRemoteNowPlayingInfoArtworkData as String] as? Data {
            if artworkData != lastArtworkData {
                lastCoverBase64 = JsonSerializer.encodeCoverJpeg(artworkData)
                lastArtworkData = artworkData
            }
            coverBase64 = lastCoverBase64
        } else {
            lastArtworkData = nil
            lastCoverBase64 = ""
        }

        let sourceIconBase64 = JsonSerializer.encodeAppIcon(bundleId: bundleId)

        return BridgeState(
            type: "state",
            state: playbackState,
            title: title,
            artist: artist,
            albumTitle: album,
            position: elapsed,
            duration: duration,
            bundleId: bundleId,
            isActive: isActive,
            coverBase64: coverBase64,
            sourceIconBase64: sourceIconBase64
        )
    }
}

struct BridgeState: Codable {
    let type: String
    let state: String
    let title: String
    let artist: String
    let albumTitle: String
    let position: Double
    let duration: Double
    let bundleId: String
    let isActive: Bool
    let coverBase64: String
    let sourceIconBase64: String

    static func inactive() -> BridgeState {
        BridgeState(type: "state", state: "stopped", title: "", artist: "",
                    albumTitle: "", position: 0, duration: 0, bundleId: "",
                    isActive: false, coverBase64: "", sourceIconBase64: "")
    }
}
