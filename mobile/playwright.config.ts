import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e/tests',
  timeout: 30000,
  expect: {
    timeout: 5000
  },
  fullyParallel: true,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://127.0.0.1:8100',
    viewport: { width: 390, height: 844 },
    trace: 'on-first-retry'
  },
  webServer: {
    command: 'npm run start -- --host=127.0.0.1 --port=8100 --no-open --configuration=e2e',
    cwd: __dirname,
    timeout: 120000,
    reuseExistingServer: !process.env.CI
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
