#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>

/// Forces AVAudioSessionCategoryPlayback so game audio is not silenced by the
/// iPhone Ring/Silent hardware switch (Ambient would be muted).
extern "C" void CornMaze_ConfigurePlaybackAudioSession(void)
{
    AVAudioSession *session = [AVAudioSession sharedInstance];
    NSError *error = nil;
    BOOL ok = [session setCategory:AVAudioSessionCategoryPlayback error:&error];
    if (!ok && error != nil)
        NSLog(@"CornMaze audio session category failed: %@", error);
    error = nil;
    ok = [session setActive:YES error:&error];
    if (!ok && error != nil)
        NSLog(@"CornMaze audio session activate failed: %@", error);
}

extern "C" void CornMaze_ReactivateAudioSession(void)
{
    NSError *error = nil;
    BOOL ok = [[AVAudioSession sharedInstance] setActive:YES error:&error];
    if (!ok && error != nil)
        NSLog(@"CornMaze audio session reactivate failed: %@", error);
}
