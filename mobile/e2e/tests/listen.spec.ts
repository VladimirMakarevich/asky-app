import { test, expect } from '@playwright/test';

test.describe('Listen flow', () => {
  test('starts listening and receives transcripts', async ({ page }) => {
    await page.goto('/');

    await expect(page.locator('ion-chip')).toHaveText(/Подключено|Ожидает/);

    const toggle = page.locator('ion-fab-button');
    await toggle.click();

    const finalTranscript = page.locator('ion-item h3', { hasText: 'Финальный фрагмент' });
    await expect(finalTranscript).toBeVisible({ timeout: 5000 });

    await toggle.click();
    await expect(toggle).toBeEnabled();
  });
});
