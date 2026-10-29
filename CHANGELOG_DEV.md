# Developer Changelog

Technical history generated from Git by `tools/changelog`. Do not edit generated files manually.

[Full archive](docs/changelog/README.md) · [Classification report](docs/changelog/report.json) · [Unreleased comparison](https://github.com/Astro-Rift-Games/ProjectGrimhold/commits/New-Testing)

## Systems at a glance

856 canonical entries. Counts overlap when a commit has evidence for multiple systems; they are not additive.

| System | Features | Fixes | Refactors / perf | Infrastructure | Secondary | Reverts / other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Abilities | 70 | 5 | 0 | 0 | 17 | 6 |
| Art & Animation | 111 | 22 | 7 | 2 | 7 | 51 |
| Audio | 19 | 1 | 1 | 0 | 2 | 5 |
| Build & Tooling | 289 | 49 | 17 | 2 | 26 | 40 |
| Combat | 174 | 24 | 10 | 1 | 50 | 45 |
| Documentation | 207 | 37 | 9 | 0 | 41 | 34 |
| Extraction | 56 | 16 | 8 | 0 | 15 | 22 |
| Input | 18 | 1 | 1 | 1 | 1 | 5 |
| Inventory & Loot | 183 | 43 | 16 | 0 | 22 | 45 |
| Movement | 52 | 7 | 3 | 1 | 7 | 16 |
| Networking & Sessions | 144 | 27 | 8 | 0 | 19 | 52 |
| Persistence & Backend | 86 | 36 | 5 | 2 | 19 | 32 |
| Tests | 329 | 72 | 17 | 1 | 34 | 49 |
| UI | 142 | 21 | 13 | 0 | 24 | 61 |
| Unassigned | 6 | 1 | 0 | 0 | 4 | 6 |
| World & Visibility | 65 | 12 | 3 | 0 | 14 | 12 |

## Recent periods

At most 6 periods and 3 concrete primary headlines per period. Documentation, tests, maintenance, ambiguous messages and all original bodies remain in the archive.

### [2026-10 — Unreleased history](docs/changelog/history-2026-10.md)

199 features; 57 bug fixes; 6 refactoring; 1 architecture & infrastructure; 74 documentation; 23 tests; 5 maintenance; 16 other changes. 383 original commits in detail.

- **presentation:** drain render event queues without subscribers ([59cada5](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/59cada58019373ad7ffb23ef9ab5494f3d461da0))
- **loot:** collect world pickups partially when capacity runs out ([2e026ea](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/2e026eabf23d52e7be527718f9f37cb9c8d59152))
- **minimap:** resolve the minimap layout from the loaded Raid scene ([83f6422](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/83f64225817dfdfcc36b08c818945a289f1971d2))

### [2026-09 — Unreleased history](docs/changelog/history-2026-09.md)

111 features; 22 bug fixes; 10 refactoring; 6 documentation; 2 tests; 5 maintenance; 66 other changes. 223 original commits in detail.

- **dungeon-pressure:** add phase change audio presenter and fix networked property weaving ([4176aeb](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/4176aeb3569bdca0b3d3f71c3f097322896a4922))
- **dungeon-pressure:** implement enemy reinforcement director ([a447dc9](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/a447dc95be169d5b60dace2ea8c4a6b760e2b032))
- **dungeon-pressure:** implement active PvE population tracking ([1a18a05](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/1a18a057a77cef0820313837d2cfa9d6eaebd044))

### [2026-08 — Unreleased history](docs/changelog/history-2026-08.md)

89 features; 11 bug fixes; 7 refactoring; 3 architecture & infrastructure; 4 documentation; 1 tests; 1 maintenance; 54 other changes. 170 original commits in detail.

- Add the assets from the equipment sets ([126524a](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/126524ab9512971916fed9a8aa13d3d2643ea0c1))
- **progression:** add Town attribute assignment UI ([2b85929](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/2b8592937b329dc5b1850294e8f26e7a29781f71))
- **progression:** implement attribute point assignment ([9f8b8be](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/9f8b8becdd6947d7e3b8333915406a921cf6f538))

### [2026-07 — Unreleased history](docs/changelog/history-2026-07.md)

47 features; 2 refactoring; 1 architecture & infrastructure; 5 documentation; 3 tests; 25 other changes. 83 original commits in detail.

- add full-body damage hitboxes to characters ([0cb931f](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/0cb931f5e9c4fc631a1a53982c223000f912dc94))
- add diegetic player feedback ([2064394](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/2064394624b83128ca758479771a40fcd3396ad1))
- Implement authoritative inventory world drops ([d42ba8c](https://github.com/Astro-Rift-Games/ProjectGrimhold/commit/d42ba8c14c09d894de331885ef82dec0c486100a))

Older periods and every release are available in the [full archive index](docs/changelog/README.md).
