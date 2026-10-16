// iPhone, iPad and Apple TV: play the game's sound as playback audio, so it is heard with the
// ring/silent switch on (Unity's default "ambient" session goes quiet in silent mode), mixing with
// whatever else is playing.
#import <AVFoundation/AVFoundation.h>

extern "C" void SC_PlaybackAudio()
{
    AVAudioSession *s = [AVAudioSession sharedInstance];
    NSError *err = nil;
    [s setCategory:AVAudioSessionCategoryPlayback withOptions:AVAudioSessionCategoryOptionMixWithOthers error:&err];
    if (err) NSLog(@"[Schofield] audio session category: %@", err);
    [s setActive:YES error:&err];
    if (err) NSLog(@"[Schofield] audio session active: %@", err);
}
