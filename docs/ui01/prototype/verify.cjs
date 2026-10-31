const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { chromium } = require('playwright');

const root = __dirname;
const screenshots = path.join(root, 'screenshots');
const states = ['resident', 'workgroups', 'build', 'research', 'dynasty', 'events', 'army', 'map', 'diplomacy'];
const sizes = [
  { width: 1920, height: 1080, tag: 'desktop' },
  { width: 1280, height: 720, tag: 'minimum' }
];

function ensure(condition, message) {
  if (!condition) throw new Error(message);
}

(async () => {
  fs.mkdirSync(screenshots, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const report = [];

  try {
    for (const size of sizes) {
      const page = await browser.newPage({ viewport: size, deviceScaleFactor: 1 });
      const errors = [];
      page.on('console', message => {
        if (message.type() === 'error') errors.push(`console: ${message.text()}`);
      });
      page.on('pageerror', error => errors.push(`page: ${error.message}`));
      await page.goto(pathToFileURL(path.join(root, 'index.html')).href);

      for (const state of states) {
        await page.evaluate(name => show(name), state);
        await page.screenshot({ path: path.join(screenshots, `${size.tag}-${state}.png`) });
      }

      for (const state of ['settlement', 'resident', 'workgroups', 'build', 'research', 'dynasty', 'army', 'events', 'map']) {
        await page.locator(`[data-go=${state}]`).first().click();
        ensure(await page.locator('.prototype').getAttribute('data-screen') === state,
          `${size.tag}: navigation to ${state} failed`);
      }

      await page.evaluate(() => show('resident'));
      ensure(await page.locator('.quick-group[data-group]').count() === 7 &&
        await page.locator('.quick-group[data-group-filter]').count() === 1,
        `${size.tag}: scalable group tracker is incomplete`);
      await page.locator('[data-group=researchers]').click();
      ensure(await page.locator('.group-members').getAttribute('class').then(x => x.includes('open')),
        `${size.tag}: group member picker did not open`);
      await page.screenshot({ path: path.join(screenshots, `${size.tag}-group-members.png`) });
      await page.locator('[data-resident=elda]').click();
      ensure(await page.locator('.prototype').getAttribute('data-selected-resident') === 'elda',
        `${size.tag}: group member selection failed`);
      ensure((await page.locator('.context-sheet').textContent()).includes('Эльда Серая'),
        `${size.tag}: selected resident was not shown`);

      await page.evaluate(() => show('workgroups'));
      const groupRules = await page.locator('.workspace').textContent();
      ensure(groupRules.includes('Настроить работы') && groupRules.includes('Сменить командира') &&
        groupRules.includes('06:00–18:00') && groupRules.includes('Высокий'),
        `${size.tag}: player-facing group controls are incomplete`);
      ensure(!groupRules.includes('Чужие задачи') && !groupRules.includes('Прерывает только строителей') &&
        !groupRules.includes('Прерванная работа') && !groupRules.includes('Профессия и группа совпадают'),
        `${size.tag}: internal scheduling rules leaked into player-facing copy`);

      await page.evaluate(() => show('research'));
      ensure(await page.locator('.tech-node').count() >= 12,
        `${size.tag}: research dependency tree is incomplete`);
      await page.locator('[data-tech-filter=defense]').click();
      ensure(await page.locator('.prototype').getAttribute('data-tech-filter') === 'defense',
        `${size.tag}: research category filter failed`);

      await page.evaluate(() => show('build'));
      const buildDrawer = await page.locator('.workspace.build-drawer').boundingBox();
      ensure(buildDrawer && buildDrawer.height <= 300 && buildDrawer.y > size.height * .4,
        `${size.tag}: construction catalog obscures too much of the world`);

      await page.evaluate(() => show('resident'));
      await page.locator('.mini-map').click();
      ensure(await page.locator('.prototype').getAttribute('data-screen') === 'map',
        `${size.tag}: mini-map navigation failed`);

      await page.keyboard.press('9');
      ensure(await page.locator('.prototype').getAttribute('data-screen') === 'events',
        `${size.tag}: keyboard navigation failed`);
      await page.locator('[data-go=map]').first().click();
      await page.locator('.workspace-head .action').click();
      ensure(await page.locator('.prototype').getAttribute('data-screen') === 'diplomacy',
        `${size.tag}: map to diplomacy navigation failed`);

      const layout = await page.evaluate(() => ({
        viewportWidth: innerWidth,
        bodyWidth: document.body.scrollWidth,
        prototypeWidth: document.querySelector('.prototype').scrollWidth,
        visibleButtons: [...document.querySelectorAll('button')].filter(x => x.offsetParent).length
      }));
      ensure(layout.bodyWidth === layout.viewportWidth && layout.prototypeWidth === layout.viewportWidth,
        `${size.tag}: horizontal overflow detected`);
      ensure(errors.length === 0, `${size.tag}: ${errors.join('; ')}`);
      report.push({ size, statesRendered: states.length, supplementaryRenders: 1, layout, errors });
      await page.close();
    }
  } finally {
    await browser.close();
  }

  fs.writeFileSync(path.join(root, 'verification-report.json'), JSON.stringify({ passed: true, report }, null, 2));
  console.log(`UI01 prototype verification passed: ${sizes.length} viewports, ${(states.length + 1) * sizes.length} screenshots.`);
})().catch(error => {
  console.error(error);
  process.exit(1);
});
