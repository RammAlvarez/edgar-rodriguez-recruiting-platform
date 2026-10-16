import { bootstrapApplication } from '@angular/platform-browser';
import { loadRuntimeConfig } from '@platform/api-client';
import { createAppConfig } from './app/app.config';
import { App } from './app/app';
import { createFirebaseAuth } from './auth/firebase-auth';

const config = await loadRuntimeConfig();
bootstrapApplication(App, createAppConfig(config, createFirebaseAuth(config))).catch((err) => console.error(err));
