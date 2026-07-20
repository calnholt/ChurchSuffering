import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';
import { defineConfig } from 'vite';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const contentDir = path.resolve(__dirname, '../Content');
const publicPortraitDir = path.resolve(__dirname, 'public/game-content');
const BACKGROUND_FILE = 'desert_background_location.png';

function enemyIdToAssetName(enemyId) {
  return enemyId
    .split('_')
    .map((part) => (part ? part.charAt(0).toUpperCase() + part.slice(1) : part))
    .join('_');
}

function syncBackground() {
  const source = path.join(contentDir, BACKGROUND_FILE);
  const dest = path.join(publicPortraitDir, BACKGROUND_FILE);
  if (fs.existsSync(source)) {
    fs.copyFileSync(source, dest);
  }
}

function syncStaticAssets() {
  fs.mkdirSync(publicPortraitDir, { recursive: true });
  syncBackground();

  for (const fileName of ['crusader_sword.png']) {
    const source = path.join(contentDir, fileName);
    const dest = path.join(publicPortraitDir, fileName);
    if (fs.existsSync(source)) {
      fs.copyFileSync(source, dest);
    }
  }
}

function syncEnemyPortraits() {
  syncStaticAssets();

  const enemiesPath = path.resolve(__dirname, 'src/data/enemies.json');
  const enemies = JSON.parse(fs.readFileSync(enemiesPath, 'utf-8'));

  for (const enemy of enemies) {
    const fileName = `${enemyIdToAssetName(enemy.id)}.png`;
    const source = path.join(contentDir, 'Enemies', fileName);
    const dest = path.join(publicPortraitDir, fileName);
    if (fs.existsSync(source)) {
      fs.copyFileSync(source, dest);
    }
  }
}

function serveGameContent(server) {
  server.middlewares.use('/game-content', (req, res, next) => {
    const requested = decodeURIComponent((req.url ?? '').replace(/^\//, ''));
    if (!requested || requested.includes('..')) {
      next();
      return;
    }

    const publicPath = path.join(publicPortraitDir, requested);
    const enemiesContentPath = path.join(contentDir, 'Enemies', requested);
    const contentPath = path.join(contentDir, requested);
    const filePath = fs.existsSync(publicPath)
      ? publicPath
      : fs.existsSync(enemiesContentPath)
        ? enemiesContentPath
        : contentPath;

    if (
      !filePath.startsWith(publicPortraitDir)
      && !filePath.startsWith(contentDir)
    ) {
      next();
      return;
    }

    if (!fs.existsSync(filePath)) {
      next();
      return;
    }

    res.setHeader('Content-Type', 'image/png');
    fs.createReadStream(filePath).pipe(res);
  });
}

export default defineConfig({
  server: {
    fs: { allow: ['..'] },
  },
  plugins: [
    {
      name: 'enemy-portrait-assets',
      buildStart() {
        syncEnemyPortraits();
      },
      configureServer(server) {
        syncEnemyPortraits();
        serveGameContent(server);
      },
      configurePreviewServer(server) {
        serveGameContent(server);
      },
    },
  ],
});
