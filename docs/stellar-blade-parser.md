# Stellar Blade Save Parser - Documentação

## Visão Geral
O parser de save do Stellar Blade lê o arquivo binário do jogo para extrair informações de conquistas (achievements) sem precisar da Steam API. O parser **APENAS LE** o save — nunca modifica.

## Localização do Save
```
C:\Users\{username}\AppData\Local\SB\Saved\SaveGames\{steamId}\StellarBladeSave00.sav
```

## Formato do Arquivo
- **Engine**: Unreal Engine 4 (UE4) — Release 4.26
- **Tamanho**: ~10 MB (10.052.406 bytes)
- **Header**: "EVAS" (primeiros 4 bytes)
- **Steam ID**: Armazenado como FString perto do final do arquivo (offset ~10.052.362), precedido por uint32 length=18

## Estrutura de Propriedades UE4
Cada propriedade segue o formato:
```
[NomeASCII\0] [TipoASCII\0] [Size(4 bytes LE)] [ArrayIndex(4 bytes LE)] [Valor...]
```

### Tipos de Propriedade
| Tipo | Offset do Valor | Descrição |
|------|----------------|-----------|
| `BoolProperty` | +21 bytes do nome | 1 byte: `0x00` = falso, `0x01` = verdadeiro |
| `UInt32Property` | +23 bytes do nome | 4 bytes: inteiro sem sinal little-endian |
| `StrProperty` | variável | FString: [length(4)] [ASCII] [null] |

### Fórmula de Offset
```
BoolProperty:   offset_nome + 13 ("BoolProperty\0") + 4 (size) + 4 (arrayIndex) = +21
UInt32Property: offset_nome + 15 ("UInt32Property\0") + 4 (size) + 4 (arrayIndex) = +23
```

## Trophy Flags → Steam Achievements
O parser extrai 25 trophy flags do tipo `Trophy_*`. Cada flag contém:
- `bCompleted` (BoolProperty): Se a conquista foi desbloqueada
- `ProgressValue` (UInt32Property): Contador de progresso

### Mapeamento Completo
| Trophy Flag | Steam Achievement |
|-------------|-------------------|
| `Trophy_Platinum` | EVE Protocol |
| `Trophy_Activate_FirstCamp` | Camp Preparation |
| `Trophy_Activate_AllCamp` | Meticulous Explorer |
| `Trophy_KillCharacter` | Cruel Liberator |
| `Trophy_KillCharacter_Brute` | Brute |
| `Trophy_KillCharacter_AllNative` | Naytiba Researcher |
| `Trophy_Acquire_AllNanoSuit` | Nano Suit Collector |
| `Trophy_Acquire_AllSkill` | Thorough Technician |
| `Trophy_Acquire_AllSkill_v2` | Infinite Blade |
| `Trophy_Acquire_AllCan` | Can Collector |
| `Trophy_Acquire_AllRecords` | Records Collector |
| `Trophy_Open_AllBox` | Box Hunter |
| `Trophy_CompleteLevel_AltesLabor` | Altess Levoire |
| `Trophy_LevelUpMax_AllExoSpine` | Perfect Exospine |
| `Trophy_WeaponMaxUpgrade` | Perfect Blood Edge |
| `Trophy_TumblerMaxUpgrade` | Perfect Rechargeable Tumbler |
| `Trophy_BodyMaxUpgrade` | Perfect Physical Enhancement |
| `Trophy_BetaMaxUpgrade` | Perfect Beta Energy Enhancement |
| `Trophy_UseItem_Gold_At_Shop` | Shopper |
| `Trophy_CharKill_BetaSkill` | Naytiba Hunter |
| `Trophy_CharKill_BurstSkill` | Relentless Destroyer |
| `Trophy_CharKill_RangeSkill` | Cold-blooded Sniper |
| `Trophy_CharKill_AssassinationSkills` | Silent Executioner |
| `Trophy_JustEvade` | Battlefield Martial Artist |
| `Trophy_JustParry` | Agile Gladiator |

### Resultado do Usuário (6/25 completadas)
```
✓ Camp Preparation (Trophy_Activate_FirstCamp) progress: 256
✓ Brute (Trophy_KillCharacter_Brute) progress: 256
✓ Altess Levoire (Trophy_CompleteLevel_AltesLabor) progress: 256
✓ Silent Executioner (Trophy_CharKill_AssassinationSkills) progress: 12800
✓ Battlefield Martial Artist (Trophy_JustEvade) progress: 51200
✓ Agile Gladiator (Trophy_JustParry) progress: 76800
```

## Endings
| Flag | Achievement |
|------|-------------|
| `EndingTimeStamp_KillElder` | Making New Memories |
| `EndingTimeStamp_KillLily` | Cost of Lost Memories |
| `EndingTimeStamp_SaveLily` | Return to the Colony |

## Outros Dados Extraídos
- **Quest Completions**: `Complete_Quest_*` → Beyond Fate, Sisterly Love, Beep!
- **New Game Plus Count**: `NewGamePlusPlayCount` (UInt32)

## Como Usar
1. O parser é chamado automaticamente quando o jogo Steam AppID 3489700 é detectado
2. O componente React `StellarBladeAchievements.tsx` exibe os dados
3. Dados são lidos em tempo real a cada clique em "Atualizar"

## Segurança
- **NUNCA** modifica o arquivo de save
- **NUNCA** cria cópias ou backups (save original é preservado)
- **APENAS** leitura binária via `fs.readFileSync()`
