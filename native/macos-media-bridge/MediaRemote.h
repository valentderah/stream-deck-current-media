#ifndef MediaRemote_h
#define MediaRemote_h

#include <CoreFoundation/CoreFoundation.h>

// Playback state keys
extern CFStringRef kMRMediaRemoteNowPlayingApplicationIsPlaying;
extern CFStringRef kMRMediaRemoteNowPlayingInfoAlbum;
extern CFStringRef kMRMediaRemoteNowPlayingInfoArtist;
extern CFStringRef kMRMediaRemoteNowPlayingInfoTitle;
extern CFStringRef kMRMediaRemoteNowPlayingInfoArtworkData;
extern CFStringRef kMRMediaRemoteNowPlayingInfoElapsedTime;
extern CFStringRef kMRMediaRemoteNowPlayingInfoDuration;
extern CFStringRef kMRMediaRemoteNowPlayingApplicationBundleIdentifier;
extern CFStringRef kMRMediaRemoteNowPlayingInfoPlaybackRate;

// Commands
enum {
    kMRMediaRemoteCommandPlay = 0,
    kMRMediaRemoteCommandPause = 1,
    kMRMediaRemoteCommandTogglePlayPause = 2,
    kMRMediaRemoteCommandNextTrack = 4,
    kMRMediaRemoteCommandPreviousTrack = 5,
    kMRMediaRemoteCommandChangePlaybackPosition = 25,
};

extern CFStringRef kMRMediaRemoteOptionPlaybackPosition;

// Notifications
extern CFStringRef kMRMediaRemoteNowPlayingInfoDidChangeNotification;
extern CFStringRef kMRMediaRemotePlaybackStateDidChangeNotification;

void MRMediaRemoteRegisterForNowPlayingNotifications(dispatch_queue_t queue);
CFDictionaryRef MRMediaRemoteGetNowPlayingInfo(dispatch_queue_t queue);
void MRMediaRemoteSendCommand(int command, CFDictionaryRef options);
void MRMediaRemoteGetNowPlayingApplicationPID(dispatch_queue_t queue, int *pid);

#endif
