import Foundation
import AppKit

enum MRKeys {
    static let nowPlayingInfoDidChange = "kMRMediaRemoteNowPlayingInfoDidChangeNotification"
    static let playbackStateDidChange = "kMRMediaRemotePlaybackStateDidChangeNotification"
    static let title = "kMRMediaRemoteNowPlayingInfoTitle"
    static let artist = "kMRMediaRemoteNowPlayingInfoArtist"
    static let album = "kMRMediaRemoteNowPlayingInfoAlbum"
    static let artworkData = "kMRMediaRemoteNowPlayingInfoArtworkData"
    static let elapsedTime = "kMRMediaRemoteNowPlayingInfoElapsedTime"
    static let duration = "kMRMediaRemoteNowPlayingInfoDuration"
    static let bundleId = "kMRMediaRemoteNowPlayingApplicationBundleIdentifier"
    static let playbackRate = "kMRMediaRemoteNowPlayingInfoPlaybackRate"
    static let playbackPosition = "kMRMediaRemoteOptionPlaybackPosition"
}

final class MediaRemoteBridge {
    static let shared = MediaRemoteBridge()

    private let client = MediaRemoteClient.shared
    private var lastElapsedTime: Double = 0
    private var lastArtworkData: Data?
    private var lastCoverBase64: String = ""

    func start() {
        guard client.isAvailable else {
            fputs("ERROR:MediaRemote framework unavailable\n", stderr)
            return
        }

        client.registerForNotifications(on: .main)

        let center = DistributedNotificationCenter.default()
        center.addObserver(
            forName: NSNotification.Name(MRKeys.nowPlayingInfoDidChange),
            object: nil, queue: .main
        ) { _ in self.emitCurrentState() }
        center.addObserver(
            forName: NSNotification.Name(MRKeys.playbackStateDidChange),
            object: nil, queue: .main
        ) { _ in self.emitCurrentState() }
    }

    func emitCurrentState() {
        client.fetchNowPlayingInfo { [weak self] info in
            guard let self else { return }
            let state = self.buildState(from: info)
            JsonSerializer.emit(state)
        }
    }

    func getElapsedTime(completion: @escaping (Double) -> Void) {
        if let info = client.cachedNowPlayingInfo(),
           let elapsed = info[MRKeys.elapsedTime] as? Double {
            lastElapsedTime = elapsed
            completion(elapsed)
            return
        }

        client.fetchNowPlayingInfo { [weak self] info in
            guard let self else { return }
            if let elapsed = info?[MRKeys.elapsedTime] as? Double {
                self.lastElapsedTime = elapsed
            }
            completion(self.lastElapsedTime)
        }
    }

    func handleCommand(_ line: String) {
        guard line.hasPrefix("cmd:") else { return }
        let cmd = String(line.dropFirst(4))

        switch cmd {
        case "play_pause":
            client.send(Int32(kMRMediaRemoteCommandTogglePlayPause))
        case "next":
            client.send(Int32(kMRMediaRemoteCommandNextTrack))
        case "previous":
            client.send(Int32(kMRMediaRemoteCommandPreviousTrack))
        case "seek_forward":
            seek(by: 10.0)
        case "seek_backward":
            seek(by: -10.0)
        default:
            break
        }
    }

    private func seek(by offset: Double) {
        getElapsedTime { [weak self] elapsed in
            guard let self else { return }
            let newPos = max(0, elapsed + offset)
            let options = [MRKeys.playbackPosition: newPos] as CFDictionary
            self.client.send(Int32(kMRMediaRemoteCommandChangePlaybackPosition), options: options)
        }
    }

    private func buildState(from info: [String: Any]?) -> BridgeState {
        guard let info else {
            return BridgeState.inactive()
        }

        let title = info[MRKeys.title] as? String ?? ""
        let artist = info[MRKeys.artist] as? String ?? ""
        let album = info[MRKeys.album] as? String ?? ""
        let bundleId = info[MRKeys.bundleId] as? String ?? ""
        let elapsed = info[MRKeys.elapsedTime] as? Double ?? 0
        let duration = info[MRKeys.duration] as? Double ?? 0
        let rate = info[MRKeys.playbackRate] as? Double ?? 0

        let hasTrackData = !title.isEmpty || !artist.isEmpty
        let isRunning = bundleId.isEmpty ? false :
            NSWorkspace.shared.runningApplications.contains { $0.bundleIdentifier == bundleId }
        let isActive = (hasTrackData && isRunning) || (hasTrackData && rate > 0)

        let playbackState: String
        if rate > 0 { playbackState = "playing" }
        else if hasTrackData { playbackState = "paused" }
        else { playbackState = "stopped" }

        var coverBase64 = ""
        if let artworkData = info[MRKeys.artworkData] as? Data, !artworkData.isEmpty {
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
