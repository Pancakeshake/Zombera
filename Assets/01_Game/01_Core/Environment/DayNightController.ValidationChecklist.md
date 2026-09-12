# DayNight Controller Validation Checklist

Run these checks in Play Mode before and after refactor changes.

- [ ] Hour progression: let time run 2-3 in-game hours at 1x and verify CurrentHour steadily advances.
- [ ] Day rollover: set time close to 24:00, let it pass midnight, verify DayNumber increments by 1 and hour wraps to 00:xx.
- [ ] Phase transitions: move time across 05:00, 07:00, 18:00, and 21:00 and verify phase transitions Dawn -> Day -> Dusk -> Night.
- [ ] Enviro absent path: run in a scene without Enviro and verify sun rotation, ambient, fog, and skybox values still update.
- [ ] Enviro present path: run in a scene with Enviro enabled and verify detection, Enviro time sync, and stable visuals.
- [ ] API compatibility: verify UI and debug tools still use Instance, CurrentHour, DayNumber, CurrentPhase, and SetHour with no behavior regression.
