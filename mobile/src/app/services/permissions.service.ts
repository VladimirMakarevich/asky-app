import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PermissionsService {
  async ensureMicrophonePermission(): Promise<boolean> {
    if (!(navigator?.mediaDevices?.getUserMedia)) {
      return false;
    }

    const navWithPermissions = navigator as Navigator & {
      permissions?: {
        query(options: { name: PermissionName }): Promise<PermissionStatus>;
      };
    };

    try {
      if (navWithPermissions.permissions?.query) {
        const status = await navWithPermissions.permissions.query({ name: 'microphone' });
        if (status.state === 'granted') {
          return true;
        }
        if (status.state === 'denied') {
          return false;
        }
      }
    } catch (error) {
      console.warn('Microphone permission check failed', error);
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      stream.getTracks().forEach((track) => track.stop());
      return true;
    } catch (error) {
      console.warn('Microphone permission request denied', error);
      return false;
    }
  }
}
