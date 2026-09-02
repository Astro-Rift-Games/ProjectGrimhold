const fs = require('fs');
const path = require('path');

const projectRoot = 'e:/Programs/Unity/Projects/AstroRiftGames/ProjectGrimhold/Project Grimhold/Assets';
const catalogPath = path.join(projectRoot, 'Scriptable Objects/RaidLootValueCatalog.asset');

const content = fs.readFileSync(catalogPath, 'utf8');
const guids = [...content.matchAll(/guid: ([a-f0-9]{32})/g)].map(m => m[1]);

console.log(`Found ${guids.length} GUIDs in catalog.`);

function findMetaFileWithGuid(dir, guid) {
    const files = fs.readdirSync(dir);
    for (const file of files) {
        const fullPath = path.join(dir, file);
        if (fs.statSync(fullPath).isDirectory()) {
            if (findMetaFileWithGuid(fullPath, guid)) return true;
        } else if (fullPath.endsWith('.meta')) {
            const metaContent = fs.readFileSync(fullPath, 'utf8');
            if (metaContent.includes(`guid: ${guid}`)) {
                return true;
            }
        }
    }
    return false;
}

for (const guid of guids) {
    if (!findMetaFileWithGuid(projectRoot, guid)) {
        console.log(`MISSING: ${guid}`);
    }
}
console.log('Done.');
