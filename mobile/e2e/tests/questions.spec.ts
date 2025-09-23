import { test, expect } from '@playwright/test';

test.describe('Questions flow', () => {
  test('retrieves and manages suggested questions', async ({ page }) => {
    await page.goto('/');

    const requestButton = page.getByRole('button', { name: 'Получить вопросы' });
    await requestButton.click();

    const questionList = page.locator('ion-item h3');
    await expect(questionList.first()).toContainText('Что самое важное');

    const askButton = page.getByRole('button', { name: 'Задать' }).first();
    await askButton.click();

    const hideButton = page.getByRole('button', { name: 'Скрыть' }).first();
    await expect(hideButton).toBeVisible();

    const showMore = page.getByRole('button', { name: 'Показать ещё' });
    await showMore.click();
    await expect(questionList).toHaveCount(4);
  });
});
