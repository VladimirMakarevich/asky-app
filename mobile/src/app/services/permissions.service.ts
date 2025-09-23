import { Injectable } from '@angular/core';
import { VoiceRecorder } from '@capacitor-community/voice-recorder';
import { Capacitor } from '@capacitor/core';

@Injectable({ providedIn: 'root' })
export class PermissionsService {
  async ensureMicrophonePermission(): Promise<boolean> {
    if (!Capacitor.isPluginAvailable('VoiceRecorder')) {
      // Allow web fallback where browser permission prompt will be used.
      return true;
    }

    const hasPermission = await VoiceRecorder.hasAudioRecordingPermission();
    if (hasPermission.value) {
      return true;
    }

    const status = await VoiceRecorder.requestAudioRecordingPermission();
    return status.value ?? false;
  }
}
