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

// diagnostics: what the OS thinks the audio session is doing (debug builds log it)
extern "C" const char* SC_AudioReport()
{
    AVAudioSession *s = [AVAudioSession sharedInstance];
    NSMutableString *r = [NSMutableString stringWithFormat:@"cat=%@ mode=%@ opts=%lu rate=%.0f outCh=%ld vol=%.2f other=%d silenceHint=%d", s.category, s.mode, (unsigned long)s.categoryOptions, s.sampleRate, (long)s.outputNumberOfChannels, s.outputVolume, s.isOtherAudioPlaying, s.secondaryAudioShouldBeSilencedHint];
    for (AVAudioSessionPortDescription *p in s.currentRoute.outputs) [r appendFormat:@" out=%@(%@,%lu ch)", p.portName, p.portType, (unsigned long)p.channels.count];
    return strdup([r UTF8String]);
}

// Apple TV: long-form playback session (AirPlay / HomePod routes want it), stereo out. The engine is
// restarted afterwards from C# so it opens on the new route.
extern "C" void SC_TVAudio()
{
    AVAudioSession *s = [AVAudioSession sharedInstance];
    NSError *err = nil;
    if (@available(tvOS 11.0, iOS 11.0, *))
        [s setCategory:AVAudioSessionCategoryPlayback mode:AVAudioSessionModeDefault routeSharingPolicy:AVAudioSessionRouteSharingPolicyLongFormAudio options:0 error:&err];
    else
        [s setCategory:AVAudioSessionCategoryPlayback error:&err];
    if (err) { NSLog(@"[Schofield] tv category: %@", err); err = nil; }
    [s setActive:YES error:&err];
    if (err) { NSLog(@"[Schofield] tv active: %@", err); err = nil; }
    NSInteger want = MIN((NSInteger)2, s.maximumOutputNumberOfChannels);
    if (want > 0) [s setPreferredOutputNumberOfChannels:want error:&err];
    if (err) NSLog(@"[Schofield] tv channels: %@", err);
}
