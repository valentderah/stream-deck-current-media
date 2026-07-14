import Foundation

// Background stdin reader
DispatchQueue.global(qos: .userInitiated).async {
    while let line = readLine(strippingNewline: true) {
        let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
        DispatchQueue.main.async {
            MediaRemoteBridge.shared.handleCommand(trimmed)
        }
    }
}

// Start bridge and emit initial state
MediaRemoteBridge.shared.start()
DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
    MediaRemoteBridge.shared.emitCurrentState()
}

RunLoop.main.run()
