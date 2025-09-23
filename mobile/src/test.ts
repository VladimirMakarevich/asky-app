import '@angular/compiler';
import 'zone.js/testing';
import { getTestBed } from '@angular/core/testing';
import { provideIonicAngularTesting } from '@ionic/angular/testing';

import {
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting
} from '@angular/platform-browser-dynamic/testing';

getTestBed().initTestEnvironment(
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting(),
  {
    providers: [provideIonicAngularTesting()]
  }
);
