# Stellar Blade - Save File Format Documentation

> Referência para implementação do parser de conquistas no Game Launcher.
> Analisado em: 30/06/2026
> Versão do jogo: PC (Steam AppID: 3489700)

---

## 1. Localização do Save

```
%LOCALAPPDATA%\SB\Saved\SaveGames\[SteamID]\StellarBladeSave00.sav
```

- `[SteamID]` = 17 dígitos (ex: `76561197960285355`)
- `StellarBladeSave00.sav` = Slot 1 (principal)
- `StellarBladeSave01.sav` = Slot 2
- `StellarBladeSave02.sav` = Slot 3
- `StellarBladeSave03.sav` = Slot 4
- `StellarBladeSave04.sav` = Slot 5 (máximo)
- `StellarBladeSetting.sav` = Configurações do jogo

**IMPORTANTE**: O save está vinculado à Steam ID. Ao copiar um save de outra pessoa, é necessário substituir os últimos 17 bytes do arquivo pela sua Steam ID.

---

## 2. Formato Geral do Arquivo

- **Engine**: Unreal Engine 4 (Release-4.26)
- **Magic Header**: `45 56 41 53` = "EVAS"
- **Tipo**: Binário com propriedades serializadas (UE4 Property Format)
- **Tamanho típico**: ~10MB (10.052.406 bytes analisados)
- **Steam ID**: Armazenado nos últimos 17 bytes do arquivo (ASCII)

---

## 3. Formato UE4 Property (Propriedades)

Cada propriedade no save segue o formato:

```
[4 bytes] Length (LE) - tamanho da string incluindo null terminator
[N bytes] String (ASCII)
[1 byte]  Null terminator (0x00)
```

### Exemplo: FString "ProgressValue"
```
0E 00 00 00  = length = 14 (13 chars + null)
50 72 6F 67 72 65 73 73 56 61 6C 75 65  = "ProgressValue"
00           = null terminator
```

---

## 4. Estrutura de uma Propriedade UInt32

```
[4 bytes]  Name length
[N bytes]  Name string ("Trophy_KillCharacter")
[1 byte]   Null terminator
[4 bytes]  Sub-property name length
[N bytes]  Sub-property name ("ProgressValue")
[1 byte]   Null terminator
[4 bytes]  Type name length
[N bytes]  Type name ("UInt32Property")
[1 byte]   Null terminator
[4 bytes]  Data size (sempre 04 00 00 00 = 4 bytes)
[4 bytes]  Array index (geralmente 00 00 00 00)
[4 bytes]  Value (UInt32 little-endian)
```

### Exemplo real: Trophy_Activate_FirstCamp
```
1A 00 00 00                          = name length = 26
54 72 6F 70 68 79 5F 41 63 74 69     = "Trophy_Activate_FirstCamp"
76 61 74 65 5F 46 69 72 73 74 43
61 6D 70
00                                   = null
0E 00 00 00                          = sub-prop length = 14
50 72 6F 67 72 65 73 73 56 61 6C     = "ProgressValue"
75 65
00                                   = null
0F 00 00 00                          = type length = 15
55 49 6E 74 33 32 50 72 6F 70 65     = "UInt32Property"
72 74 79
00                                   = null
04 00 00 00                          = data size = 4
00 00 00 00                          = array index = 0
XX XX XX XX                          = value (UInt32 LE)
```

---

## 5. Estrutura de uma Propriedade Bool

```
[4 bytes]  Name length
[N bytes]  Name string ("bCompleted")
[1 byte]   Null terminator
[4 bytes]  Type name length
[N bytes]  Type name ("BoolProperty")
[1 byte]   Null terminator
[4 bytes]  Data size (sempre 00 00 00 00 = 0)
[4 bytes]  Array index (geralmente 00 00 00 00)
[1 byte]   Value (0x00 = false, 0x01 = true)
```

**NOTA**: BoolProperty NÃO tem sub-property name (diferente de UInt32).

---

## 6. Trophy Flags (Conquistas Internas do Jogo)

Cada trophy no save tem DUAS propriedades:
1. `ProgressValue` (UInt32) - contador/progresso numérico
2. `bCompleted` (Bool) - se a conquista foi desbloqueada (0 ou 1)

### 6.1 Mapeamento Trophy → Steam Achievement

| Trophy Flag | Steam Achievement | Descrição |
|---|---|---|
| Trophy_Platinum | EVE Protocol | Desbloquear todas as outras conquistas |
| Trophy_Activate_FirstCamp | Camp Preparation | Ativar o primeiro camp |
| Trophy_Activate_AllCamp | Meticulous Explorer | Ativar todos os camps |
| Trophy_KillCharacter | Cruel Liberator | Derrotar 1.500 inimigos |
| Trophy_KillCharacter_Brute | Brute | Derrotar o boss Brute |
| Trophy_KillCharacter_AllNative | Naytiba Researcher | Obter info de todos os Naytibas |
| Trophy_Acquire_AllNanoSuit | Nano Suit Collector | Adquirir 30 Nano Suits |
| Trophy_Acquire_AllSkill | Thorough Technician | Aprender todas as skills |
| Trophy_Acquire_AllSkill_v2 | Infinite Blade | Aprender todas as skills no NG+ |
| Trophy_Acquire_AllCan | Can Collector | Coletar todas as latas |
| Trophy_Acquire_AllRecords | Records Collector | Coletar 200 Data Bank entries |
| Trophy_Open_AllBox | Box Hunter | Abrir 200 caixas |
| Trophy_CompleteLevel_AltesLabor | Altess Levoire | Recuperar Hyper Cell de Altess Levoire |
| Trophy_LevelUpMax_AllExoSpine | Perfect Exospine | Melhorar 10 Exospines ao máximo |
| Trophy_WeaponMaxUpgrade | Perfect Blood Edge | Melhorar Blood Edge ao máximo |
| Trophy_TumblerMaxUpgrade | Perfect Rechargeable Tumbler | Melhorar Rechargeable Tumbler ao máximo |
| Trophy_BodyMaxUpgrade | Perfect Physical Enhancement | Melhorar HP ao máximo |
| Trophy_BetaMaxUpgrade | Perfect Beta Energy Enhancement | Melhorar Beta Energy ao máximo |
| Trophy_UseItem_Gold_At_Shop | (Desconhecido) | Provavelmente usar gold na loja |
| Trophy_CharKill_BetaSkill | Naytiba Hunter | Derrotar 100 inimigos com Beta Skills |
| Trophy_CharKill_BurstSkill | Relentless Destroyer | Derrotar 50 inimigos com Burst Skills |
| Trophy_CharKill_RangeSkill | Cold-blooded Sniper | Derrotar 150 inimigos com ataques ranged |
| Trophy_CharKill_AssassinationSkills | Silent Executioner | Derrotar 50 inimigos por execução |
| Trophy_JustEvade | Battlefield Martial Artist | Perfect Dodge em 200 ataques |
| Trophy_JustParry | Agile Gladiator | Perfect Parry em 300 ataques |

### 6.2 Conquistas NÃO rastreadas por Trophy Flags

Estas conquistas são rastreadas por outros mecanismos no save:

| Steam Achievement | Como é rastreado |
|---|---|
| Abaddon | Kill_Character ou Quest_Epic |
| Corrupter | Kill_Character ou Quest_Epic |
| Gigas | Kill_Character ou Quest_Epic |
| Stalker | Kill_Character ou Quest_Epic |
| Juggernaut | Kill_Character ou Quest_Epic |
| Tachy | Kill_Character ou Quest_Epic |
| Behemoth | Kill_Character ou Quest_Epic |
| Belial | Kill_Character ou Quest_Epic |
| Karakuri | Kill_Character ou Quest_Epic |
| Demogorgon | Kill_Character ou Quest_Epic |
| Raven | Kill_Character ou Quest_Epic |
| Return to the Colony | Ending / EndingTimeStamp_SaveLily |
| Cost of Lost Memories | Ending / EndingTimeStamp_KillLily |
| Making New Memories | Ending / EndingTimeStamp_KillElder |
| Lonely Fisherman | Contagem de peixes (não identificado) |
| Beyond Fate | Quest_Sub_032 completion |
| Sisterly Love | Quest_Sub_033 completion |
| Beep! | Quest_Sub_043 completion |
| Repeating Protocols | NewGamePlusPlayCount >= 1 |
| Infinite Blade | Trophy_Acquire_AllSkill_v2 |

---

## 7. Kill Tracking (Rastreamento de Kills)

O save contém entradas do tipo:

```
Kill_Character_[Zona]_[TipoInimigo]_[Versão]
```

### Exemplos encontrados (105 entradas):
```
Kill_Character_ATL_M_Maelstrom_01      # Maelstrom em Altess Levoire
Kill_Character_DED_M_GorillaB_01       # Gorilla em DED
Kill_Character_SD_M_HedgeBoarBrute_01  # HedgeBoar Brute em SD
Kill_Character_UME_M_SkullJuggernaut_01 # Juggernaut em UME
Kill_Character_UME_M_Tachy_01          # Tachy em UME
Kill_Character_WLA_M_HedgeBoarBrute_01 # HedgeBoar Brute em WLA
Kill_Character_By_BetaSkill            # Kills por Beta Skill
Kill_Character_By_BurstSkill           # Kills por Burst Skill
Kill_Character_By_RangeSkill           # Kills por Range Attack
```

### Zonas do jogo:
- **ATL** = Altess Levoire
- **DED** = DED (Eidos districts)
- **ME** = Matrix Eleven
- **SD** = Sarcasm Dimension (?)
- **UME** = UME
- **WLA** = Wasteland Area
- **WLB** = Wasteland Area 2
- **Xion** = Xion (cidade principal)

---

## 8. Quest Completion (Completar Quests)

### Quests Épicas (história principal):
```
Complete_Quest_Quest_Epic_01   # Capítulo 1
Complete_Quest_Quest_Epic_02   # Capítulo 2
Complete_Quest_Quest_Epic_03   # Capítulo 3
Complete_Quest_Quest_Epic_04   # Capítulo 4
Complete_Quest_Quest_Epic_05   # Capítulo 5
```

### Quests de Request (secundárias):
```
Complete_Quest_Quest_Request_033
Complete_Quest_Quest_Request_034
```

### Quests Sub (side quests):
```
Complete_Quest_Quest_Sub_001   # Enya e Su's story → "Beyond Fate"
Complete_Quest_Quest_Sub_011   # D1G-g2r's story → "Beep!"
Complete_Quest_Quest_Sub_018
Complete_Quest_Quest_Sub_026
Complete_Quest_Quest_Sub_031   # Kaya's story → "Sisterly Love"
Complete_Quest_Quest_Sub_032
Complete_Quest_Quest_Sub_033
Complete_Quest_Quest_Sub_034
Complete_Quest_Quest_Sub_036
Complete_Quest_Quest_Sub_043
```

---

## 9. Endings (Finais do Jogo)

```
Ending                              # Flag geral de ending
EndingTimeStamp_KillElder           # Timestamp: Making New Memories
EndingTimeStamp_KillLily            # Timestamp: Cost of Lost Memories
EndingTimeStamp_SaveLily            # Timestamp: Return to the Colony
EndingCookieVideoIndex              # Índice do vídeo do ending
```

### Mapeamento:
- **Making New Memories** = EndingTimeStamp_KillElder (unir com Adam)
- **Cost of Lost Memories** = EndingTimeStamp_KillLily (não unir, sem 100% Lily)
- **Return to the Colony** = EndingTimeStamp_SaveLily (não unir, com 100% Lily)

---

## 10. Collectibles (Colecionáveis)

### Latas (Cans):
```
CollectCan_1, CollectCan_7, CollectCan_14, CollectCan_21,
CollectCan_28, CollectCan_35, CollectCan_42, CollectCan_49
CollectCan_AfterTPS    # Latas depois do TPS
```

### Data Bank (Records):
```
Records_ATL_Memory_01 a _05
Records_Day1_Memory_01 a _13
Records_DED10_Memory_01 a _12
Records_DED20_Memory_01 a _16
Records_DED30_Memory_01 a _06
Records_ETC_Memory_08, _09, _11, _13, _15
Records_ME01_Memory_01 a _07
Records_ME03_Memory_01, _02
Records_ME04_Memory_02 a _17
Records_ME05_Memory_01
Records_WLA_Memory_01 a _46
Records_WLB_Memory_28, _54
Records_Xion_Memory_01 a _34
Records_Quest_Request_005_01
```

---

## 11. Camps (Acampamentos)

```
DED10_Camp_00 a _07    # 8 camps em DED10
DED20_Camp_01 a _06    # 6 camps em DED20
DED30_Camp_01          # 1 camp em DED30
ME01_Camp_01 a _02     # 2 camps em ME01
ME03_Camp_01 a _02     # 2 camps em ME03
ME04_Camp_01 a _02     # 2 camps em ME04
ME05_Camp_01           # 1 camp em ME05
ME06_Camp_01           # 1 camp em ME06
WLA10_Camp_01 a _06    # 6 camps em WLA10
WLA30_Camp_01 a _03    # 3 camps em WLA30
WLA40_Camp_01          # 1 camp em WLA40
WLA50_Camp_01          # 1 camp em WLA50
WLB20_Camp_01          # 1 camp em WLB20
Xion_Camp_10 a _20     # 2 camps em Xion
```

---

## 12. New Game Plus

```
NewGamePlusPlayCount    # UInt32 - quantas vezes completou NG+
```

---

## 13. Outros Dados Importantes

### Skills:
```
CheckAmount_Skill_1 a _45    # 45 skills disponíveis
```

### Upgrades:
```
WorkShopEnhanceEve           # Upgrades da Eve
WorkShopEnhanceEveGearSocket # Upgrades de Gear
WorkShopEnhanceEveTumbler    # Upgrades do Tumbler
WorkShopEnhanceEveWeapon     # Upgrades da arma
```

### Nano Suits:
```
NanosuitMaking_1_BS_*        # Nano Suits disponíveis
```

### Ending Flags:
```
# Ending principal (0 ou 1):
Ending

# Timestamps dos endings:
EndingTimeStamp_KillElder    # Making New Memories
EndingTimeStamp_KillLily     # Cost of Lost Memories
EndingTimeStamp_SaveLily     # Return to the Colony
```

---

## 14. Script de Análise (PowerShell)

```powershell
# Analisar save do Stellar Blade
$file = "Camin\do\save\StellarBladeSave00.sav"
$bytes = [System.IO.File]::ReadAllBytes($file)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)

# Encontrar todas as trophy flags
$trophyPattern = 'Trophy_[A-Za-z0-9_]+'
$matches = [regex]::Matches($text, $trophyPattern)

foreach ($m in $matches) {
    $idx = $m.Index
    $name = $m.Value

    # Buscar bCompleted após o nome
    $bCompletedIdx = $text.IndexOf("bCompleted", $idx + $name.Length)
    if ($bCompletedIdx -ge 0 -and $bCompletedIdx -lt $idx + $name.Length + 300) {
        $boolPropIdx = $text.IndexOf("BoolProperty", $bCompletedIdx)
        if ($boolPropIdx -ge 0) {
            $valOffset = $boolPropIdx + 13 + 4 + 4  # BoolProperty\0 + size(4) + arrayIndex(4)
            $completed = $bytes[$valOffset]
            Write-Output "$name => bCompleted=$completed"
        }
    }
}
```

---

## 15. Notas para Implementação

1. **O save está em formato binário** - não é JSON nem XML
2. **Cada trophy tem duas propriedades**: ProgressValue (UInt32) e bCompleted (Bool)
3. **O valor de bCompleted é o mais importante** (0 = não completou, 1 = completou)
4. **ProgressValue** é um contador (ex: total de kills, total de latas)
5. **O save é vinculado à Steam ID** - os últimos 17 bytes são a Steam ID
6. **Backup sempre antes de manipular** - o save pode corromper
7. **O parser deve ser robusto** - offsets podem variar entre versões do jogo
8. **Usar regex para encontrar nomes** e depois parsear os bytes ao redor
9. **Priorizar bCompleted** sobre ProgressValue para status de conquista
10. **Algumas conquistas** (boss kills) são rastreadas por Kill_Character_*, não por Trophy_*

---

## 16. Estrutura de Dados para o Parser

```typescript
interface StellarBladeTrophy {
  name: string;              // Nome do trophy (ex: "Trophy_KillCharacter")
  steamAchievement: string;  // Nome da conquista Steam correspondente
  bCompleted: boolean;       // Se foi completado
  progressValue: number;     // Valor de progresso (counter)
}

interface StellarBladeSaveData {
  steamId: string;           // Steam ID (17 dígitos)
  trophies: StellarBladeTrophy[];
  questCompletions: string[]; // Quests completadas
  killTracking: Record<string, number>; // Kills por tipo
  collectCanCount: number;   // Total de latas
  recordsCount: number;      // Total de Data Bank entries
  campCount: number;         // Total de camps ativados
  endings: string[];         // Endings desbloqueados
  newGamePlusCount: number;  // Quantas vezes completou NG+
}
```

---

## 17. Referências

- **Steam AppID**: 3489700
- **Save Organizer**: https://github.com/Kahmul/SoulsSpeedruns-Save-Organizer
- **Hex Editor Online**: https://hexed.it/
- **Nexus Mods (Saves)**: https://www.nexusmods.com/stellarblade/mods/
- **TrueSteamAchievements**: https://truesteamachievements.com/game/Stellar-Blade/achievements
