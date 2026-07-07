# Stellar Blade Save Parser - EMECore

## Visao Geral
Parser C# que le o save binario do Stellar Blade (UE4) para extrair conquistas sem Steam API. **APENAS LEITURA.**

## Localizacao do Save
```
%LOCALAPPDATA%\SB\Saved\SaveGames\{steamId}\StellarBladeSave00.sav
```

## Implementacao

### Modelos (`EMECore.Core.Models`)
- `StellarBladeSaveData` — dados completos do save (steamId, trophies, endings, NG+)
- `StellarBladeTrophy` — trophy individual (name, steamAchievement, bCompleted, progressValue)
- `Achievement` — modelo de conquista para exibicao na UI

### Parser (`EMECore.Hardware.Services.StellarBladeParser`)
- `FindSavePath()` — localiza o arquivo de save
- `HasSave()` — verifica se o save existe
- `ParseSave()` — parse completo do save binario
- `ParseAchievements()` — extrai lista de `Achievement` para exibicao

### Service (`EMECore.Hardware.Services.AchievementService`)
- `GetAchievementsAsync(Game)` — se SteamAppId == "3489700", chama o StellarBladeParser

### UI (`EMECore.WinUI.Views.GameDetailPage`)
- `SetAchievements(List<Achievement>)` — exibe conquistas com barra de progresso

## Estrutura do Arquivo UE4
- Engine: Unreal Engine 4.26
- Header: "EVAS" (4 bytes)
- Tamanho: ~10 MB
- Propriedades: `[Nome\0][Tipo\0][Size(4)][ArrayIndex(4)][Valor]`

### Offsets
| Tipo | Offset do valor |
|------|----------------|
| BoolProperty | nome + 21 bytes |
| UInt32Property | nome + 23 bytes |

## Trophy Flags → Achievements (25)
| # | Trophy Flag | Achievement |
|---|------------|-------------|
| 1 | Trophy_Platinum | EVE Protocol |
| 2 | Trophy_Activate_FirstCamp | Camp Preparation |
| 3 | Trophy_Activate_AllCamp | Meticulous Explorer |
| 4 | Trophy_KillCharacter | Cruel Liberator |
| 5 | Trophy_KillCharacter_Brute | Brute |
| 6 | Trophy_KillCharacter_AllNative | Naytiba Researcher |
| 7 | Trophy_Acquire_AllNanoSuit | Nano Suit Collector |
| 8 | Trophy_Acquire_AllSkill | Thorough Technician |
| 9 | Trophy_Acquire_AllSkill_v2 | Infinite Blade |
| 10 | Trophy_Acquire_AllCan | Can Collector |
| 11 | Trophy_Acquire_AllRecords | Records Collector |
| 12 | Trophy_Open_AllBox | Box Hunter |
| 13 | Trophy_CompleteLevel_AltesLabor | Altess Levoire |
| 14 | Trophy_LevelUpMax_AllExoSpine | Perfect Exospine |
| 15 | Trophy_WeaponMaxUpgrade | Perfect Blood Edge |
| 16 | Trophy_TumblerMaxUpgrade | Perfect Rechargeable Tumbler |
| 17 | Trophy_BodyMaxUpgrade | Perfect Physical Enhancement |
| 18 | Trophy_BetaMaxUpgrade | Perfect Beta Energy Enhancement |
| 19 | Trophy_UseItem_Gold_At_Shop | Shopper |
| 20 | Trophy_CharKill_BetaSkill | Naytiba Hunter |
| 21 | Trophy_CharKill_BurstSkill | Relentless Destroyer |
| 22 | Trophy_CharKill_RangeSkill | Cold-blooded Sniper |
| 23 | Trophy_CharKill_AssassinationSkills | Silent Executioner |
| 24 | Trophy_JustEvade | Battlefield Martial Artist |
| 25 | Trophy_JustParry | Agile Gladiator |

## Endings
| Flag | Achievement |
|------|-------------|
| EndingTimeStamp_KillElder | Making New Memories |
| EndingTimeStamp_KillLily | Cost of Lost Memories |
| EndingTimeStamp_SaveLily | Return to the Colony |

## Quests
| Quest | Achievement |
|-------|-------------|
| Complete_Quest_Quest_Sub_032 | Beyond Fate |
| Complete_Quest_Quest_Sub_033 | Sisterly Love |
| Complete_Quest_Quest_Sub_043 | Beep! |

## Fluxo de Execucao
1. Usuario clica no jogo Stellar Blade na biblioteca
2. `MainWindow.UpdatePageVisibility` chama `AchievementService.GetAchievementsAsync`
3. Se SteamAppId == "3489700", o parser le o arquivo de save
4. Conquistas sao exibidas na `GameDetailPage` com barra de progresso
