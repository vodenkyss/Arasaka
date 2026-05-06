
# MUD Arasaka — Server a Dokumentace pro Testování

Tento projekt je textová MUD hra (Multi-User Dungeon) z prostředí kyberpankového mrakodrapu firmy Arasaka. Jedná se o síťovou hru postavenou na protokolu TCP, která umožňuje připojení více hráčů najednou, ukládání stavu postav do JSON souborů, souboje s NPC, kraftění předmětů a plnění úkolů.

Projekt byl vytvořen jako školní práce pro **SPŠ Ječná**.

---

## 🛠️ Jak projekt spustit (Návod pro testera / učitele)

Projekt se skládá ze síťového serveru, ke kterému se hráči připojují pomocí libovolného TCP klienta (např. PuTTY nebo vestavěný Terminál/Příkazový řádek).

### 1. Spuštění serveru
1. Otevři si projekt ve svém vývojovém prostředí (**Rider** nebo **Visual Studio**).
2. Ujisti se, že v kořenové složce spuštěné aplikace (vedle `.exe` souboru nebo v pracovním adresáři) se nacházejí konfigurační soubory **`world.json`** (definice místností a předmětů) a **`crafting.json`** (recepty na výrobu).
3. Spusť projekt (tlačítko **Run** / F5).
4. V konzoli serveru by se mělo objevit potvrzení:
   ```text
   Server spuštěn na portu 8888
   Herní svět načten: 12 místností.
   Receptury načteny.

2. Připojení klienta (Hráče)

Hra nemá vlastní grafické okno, běží přímo v terminálu klienta.

Možnost A: Přes PuTTY (Doporučeno na Windows)

Spusť PuTTY.

Jako Host Name (or IP address) zadej: 127.0.0.1 (nebo localhost).

Jako Port zadej: 8888.

Jako Connection type zaškrtni Raw.

Klikni na Open.

Možnost B: Přes Příkazový řádek / Terminál (Linux / macOS / Windows s Netcatem)

Otevři terminál a zadej příkaz:

Bash
nc 127.0.0.1 8888
