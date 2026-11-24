using BotArena.GameData;

namespace BotArena.Heroes;

public static class HeroCatalog
{
    private static readonly Dictionary<string, (float Ad, float Ap, float HealAd, float HealAp)> Ratios = new()
    {
        ["szarza"] = (0.4f, 0, 0, 0), ["uderzenie"] = (0.5f, 0, 0, 0),
        ["strzal"] = (0.6f, 0, 0, 0), ["deszcz"] = (0.45f, 0, 0, 0),
        ["skokorka"] = (0.5f, 0, 0, 0), ["okrzyk"] = (0.4f, 0, 0, 0), ["topor"] = (0.7f, 0, 0, 0),
        ["skok"] = (0.5f, 0, 0, 0), ["sztylet"] = (0.7f, 0, 0, 0), ["wir"] = (0.5f, 0, 0, 0),
        ["kula"] = (0, 0.7f, 0, 0), ["fala"] = (0, 0.5f, 0, 0), ["deszczognia"] = (0, 0.6f, 0, 0),
        ["pocisk"] = (0, 0.6f, 0, 0), ["uzdrowienie"] = (0, 0, 0, 0.5f),
        ["glowa"] = (0.2f, 0, 0, 0), ["klatwa"] = (0.04f, 0, 0, 0),
        ["obrot"] = (0.6f, 0, 0, 0), ["zadzakrwi"] = (0.4f, 0, 0, 0), ["egzekucja"] = (0.8f, 0, 0, 0),
        ["szkielet"] = (0, 0.15f, 0, 0.9f), ["golem"] = (0, 0.4f, 0, 2.5f),
        ["sluga"] = (0.2f, 0.3f, 0.6f, 1.0f), ["silabeliara"] = (0.6f, 0.9f, 0, 0),
        ["wielkakula"] = (0, 0.12f, 0, 0), ["postep"] = (0, 0.05f, 0, 0), ["totem"] = (0, 0.25f, 0, 0),
    };

    private static AbilityDef A(string key, string name, string text, AbilityKind kind, float cd, float range, float radius, int damage, int heal, float seconds, string fx)
    {
        var ability = new AbilityDef(key, name, text, kind, cd, range, radius, damage, heal, seconds, fx);
        return Ratios.TryGetValue(key, out var r) ? ability with { AdRatio = r.Ad, ApRatio = r.Ap, HealAdRatio = r.HealAd, HealApRatio = r.HealAp } : ability;
    }

    public static readonly HeroDef Knight = new("rycerz", "Rycerz Królewski", "Tarcza Myrtany", "Tank",
        "Weteran bitew pod Vengardem. Wolny, ale niezłomny — idzie na czele linii i przyjmuje ciosy za innych.",
        0, 900, 100, 60, HeroWeapon.OneHanded, GameItems.ITMW_1H_PAL_SWORD, GameItems.ITAR_PAL_M, null,
    [
        A("szarza", "Szarża", "Skok do przodu; wrogowie na drodze dostają obrażenia.", AbilityKind.Dash, 7, 700, 160, 40, 0, 0, "spellFX_PalLight"),
        A("tarcza", "Tarcza Wiary", "Przez 4 s przyjmujesz o 60% mniej obrażeń.", AbilityKind.Shield, 14, 0, 0, 60, 0, 4, "spellFX_PalLight"),
        A("uderzenie", "Uderzenie Sprawiedliwości", "Wybuch wokół ciebie: obrażenia i ogłuszenie na 1 s.", AbilityKind.Nova, 10, 0, 350, 55, 0, 1, "spellFX_PalRepelEvil"),
    ]);

    public static readonly HeroDef Hunter = new("lowca", "Łowca Cieni", "Mistrz łuku", "Strzelec",
        "Zwiadowca z Nowego Obozu. Trzyma dystans, a jego strzały znajdują cel nawet zza pleców minionów.",
        0, 520, 60, 100, HeroWeapon.Bow, GameItems.ITRW_BOW_M_02, GameItems.ITAR_LEATHER_L, GameItems.ITRW_ARROW,
    [
        A("strzal", "Strzał Przebijający", "Pocisk w najbliższego wroga przed tobą.", AbilityKind.Strike, 5, 1800, 0, 70, 0, 0, "spellFX_FireArrow"),
        A("unik", "Unik", "Szybki skok do tyłu.", AbilityKind.Dash, 9, -550, 0, 0, 0, 0, "spellFX_Light"),
        A("deszcz", "Deszcz Strzał", "Obszarowy ostrzał w miejscu najbliższego wroga.", AbilityKind.Rain, 12, 1500, 320, 60, 0, 0, "spellFX_FireRain"),
    ]);

    public static readonly HeroDef Mage = new("mag", "Mag Ognia", "Płomień Khorinis", "Mag",
        "Uczeń Kręgu Ognia. Kruchy, lecz najgroźniejszy z daleka: kula ognia, lodowa fala i deszcz płomieni.",
        500, 480, 40, 60, HeroWeapon.TwoHanded, GameItems.ITMW_2H_ROD, GameItems.ITAR_KDF_L, null,
    [
        A("kula", "Kula Ognia", "Pocisk w najbliższego wroga przed tobą.", AbilityKind.Strike, 4, 1600, 0, 80, 0, 0, "spellFX_InstantFireball"),
        A("fala", "Lodowa Fala", "Wybuch lodu wokół ciebie: obrażenia i ogłuszenie na 1,2 s.", AbilityKind.Nova, 11, 0, 400, 50, 0, 1.2f, "spellFX_IceWave"),
        A("deszczognia", "Deszcz Ognia", "Płonący deszcz w miejscu najbliższego wroga.", AbilityKind.Rain, 14, 1400, 350, 90, 0, 0, "spellFX_FireRain"),
    ]);

    public static readonly HeroDef Assassin = new("zabojca", "Zabójca", "Cień Portu", "Zabójca",
        "Nóż w ciemności. Skacze na cel, rzuca sztyletami i kręci ostrzami wokół siebie.",
        500, 560, 90, 100, HeroWeapon.OneHanded, GameItems.ITMW_ADDON_KNIFE01, GameItems.ITAR_BDT_M, null,
    [
        A("skok", "Skok Cienia", "Skok do przodu; wrogowie na drodze dostają obrażenia.", AbilityKind.Dash, 8, 900, 160, 60, 0, 0, "spellFX_Fear_Ground"),
        A("sztylet", "Rzut Sztyletem", "Pocisk w najbliższego wroga przed tobą.", AbilityKind.Strike, 3.5f, 1200, 0, 55, 0, 0, "spellFX_Bolt"),
        A("wir", "Wir Ostrzy", "Obrażenia wokół ciebie.", AbilityKind.Nova, 9, 0, 300, 70, 0, 0, "spellFX_Fear"),
    ]);

    public static readonly HeroDef Priest = new("kaplan", "Kapłan Innosa", "Światło Klasztoru", "Wsparcie",
        "Leczy sojuszników, osłania ich tarczą i razi wrogów świętym pociskiem.",
        650, 600, 70, 60, HeroWeapon.OneHanded, GameItems.ITMW_1H_BLESSED_02, GameItems.ITAR_NOV_L, null,
    [
        A("pocisk", "Święty Pocisk", "Pocisk w najbliższego wroga przed tobą.", AbilityKind.Strike, 3.5f, 1500, 0, 55, 0, 0, "spellFX_PalHolyBolt"),
        A("uzdrowienie", "Uzdrowienie", "Leczy ciebie i sojuszników w promieniu 6 m.", AbilityKind.Heal, 10, 0, 600, 0, 180, 0, "spellFX_Heal"),
        A("oslona", "Boska Tarcza", "Przez 3 s przyjmujesz o 50% mniej obrażeń.", AbilityKind.Shield, 15, 0, 0, 50, 0, 3, "spellFX_PalHeal"),
    ]);

    public static readonly HeroDef Berserker = new("barbarzynca", "Wojownik Orków", "Krew i Stal", "Wojownik",
        "Dwuręczny topór i szał bojowy. Wpada w tłum, ogłusza okrzykiem i rzuca toporem.",
        700, 1000, 120, 50, HeroWeapon.TwoHanded, GameItems.ITMW_2H_ORCAXE_01, GameItems.ITAR_DJG_M, null,
    [
        A("skokorka", "Skok Berserkera", "Skok do przodu; wrogowie na drodze dostają obrażenia.", AbilityKind.Dash, 8, 600, 180, 50, 0, 0, "spellFX_Fear_Ground"),
        A("okrzyk", "Okrzyk Bojowy", "Wybuch wokół ciebie: obrażenia i ogłuszenie na 1,2 s.", AbilityKind.Nova, 12, 0, 400, 45, 0, 1.2f, "spellFX_Fear"),
        A("topor", "Rzut Toporem", "Pocisk w najbliższego wroga przed tobą.", AbilityKind.Strike, 6, 900, 0, 90, 0, 0, "spellFX_Bolt"),
    ]);

    public static readonly HeroDef Lee = new("lee", "Lee", "Generał Najemników", "Wojownik",
        "Dawny generał króla, wrobiony i zesłany za Barierę. Dowodzi najemnikami z farmy Onara. Z każdą bitwą jego topór uderza mocniej.",
        0, 920, 110, 60, HeroWeapon.TwoHanded, GameItems.ITMW_2H_SLD_AXE, GameItems.ITAR_SLD_H, null,
    [
        A("glowa", "Głowa Najemnika",
            "Masz 3 s na uderzenie toporem: cios zadaje +20 obrażeń i +2 za każdy stack, a trafienie daje 1 stack. Stacki zostają do końca gry.",
            AbilityKind.Empower, 6, 0, 0, 20, 0, 3, "spellFX_LeeAxeGlow"),
        A("rozped", "Rozpęd", "Przez 3 s biegniesz o 30% szybciej.", AbilityKind.Haste, 12, 0, 0, 30, 0, 3, "spellFX_LeeHaste"),
        A("klatwa", "Klątwa",
            "Szybko unosisz rękę i przed tobą zapala się niebieski krąg (5 s): co pół sekundy lekko rani każdego wroga w środku.",
            AbilityKind.Curse, 10, 350, 260, 5, 0, 5, "spellFX_LeeCurseCircle"),
        A("pogrom", "Pogrom",
            "Na 8 s rośniesz (×1,3), zadajesz o 30% więcej obrażeń, masz +50% zdrowia, od razu leczysz 25%, a stacki liczą się ×1,5.",
            AbilityKind.Rampage, 60, 0, 0, 30, 25, 8, "spellFX_LeeRampage"),
    ])
    {
        Look = new GothicOnline.Api.PlayerVisual("hum_body_Naked0", 1, "Hum_Head_Pony", 20, 0, 0),
    };

    public static readonly HeroDef Diego = new("diego", "Diego", "Cień Starego Obozu", "Strzelec",
        "Przywódca Cieni ze Starego Obozu. Przeżył Barierę, bo nigdy nie stoi tam, gdzie celuje wróg — a jego strzała zawsze trafia drugi raz.",
        0, 560, 70, 110, HeroWeapon.Bow, GameItems.ITRW_BOW_M_03, GameItems.ITAR_DIEGO, GameItems.ITRW_ARROW,
    [
        A("mysliwy", "Myśliwy", "Pasywna: co trzecie trafienie z rzędu w tę samą postać lub moba zadaje 150% obrażeń.",
            AbilityKind.Passive, 0, 0, 3, 150, 0, 4, "spellFX_DiegoMark"),
        A("przewrot", "Przewrót", "Przewrót w przód o ok. 3 m.", AbilityKind.Roll, 7, 320, 0, 0, 0, 0.84f, "spellFX_DiegoRoll"),
        A("przeszywajaca", "Przeszywająca Strzała", "Napinasz łuk: następna strzała (5 s) ogłusza cel na 1,5 s.",
            AbilityKind.PierceShot, 10, 0, 0, 0, 0, 1.5f, "spellFX_DiegoArrowReady"),
        A("wielkilowca", "Wielki Łowca",
            "Na 15 s: Myśliwy zadaje 200% zamiast 150%, Przewrót daje niewidzialność w trakcie i 0,5 s po, a Przeszywająca Strzała ogłusza na 2 s.",
            AbilityKind.HunterMode, 60, 0, 0, 0, 0, 15, "spellFX_DiegoHunter"),
    ])
    {
        Look = new GothicOnline.Api.PlayerVisual("hum_body_Naked0", 2, "Hum_Head_Thief", 30, 0, 0),
        AmmoCount = 9999,
        Overlays = ["HumanS_DiegoRoll_V5.mds"],
    };

    public static readonly HeroDef Xardas = new("xardas", "Xardas", "Mroczny Mag", "Mag",
        "Najpotężniejszy z nekromantów, niegdyś członek Kręgu Ognia. Sam rzadko staje do walki — wysyła szkielety i golemy, a kto się do niego zbliży, dostaje piorunem.",
        0, 640, 60, 50, HeroWeapon.None, null, GameItems.ITAR_XARDAS, null,
    [
        A("szkielet", "Przyzwanie Szkieleta",
            "Przyzywasz szkieleta (słabe statystyki), który atakuje to samo, co ty (cel, na który patrzysz), i żyje 11 s.",
            AbilityKind.Summon, 8, 0, 0, 14, 110, 11, "spellFX_Fear_Ground"),
        A("piorun", "Piorun",
            "Pasywna: celne trafienie wrogiego gracza razi go piorunem z góry — dostaje połowę zadanych obrażeń więcej (razem 1,5×).",
            AbilityKind.Lightning, 0, 0, 0, 50, 0, 0, "spellFX_LightningFlash_HEAVENSRAGE"),
        A("ucieczka", "Ucieczka",
            "Zostawia swoją kopię (ten sam wygląd i broń), która biegnie tam, gdzie ty biegłeś, albo stoi; znika po 3 s.",
            AbilityKind.Decoy, 18, 0, 0, 0, 0, 3, "spellFX_Teleport_CAST"),
        A("golem", "Przyzwanie Golema",
            "Przyzywa golema, który żyje 30 s i walczy jak szkielet (atakuje twój cel).",
            AbilityKind.Summon, 40, 0, 0, 45, 700, 30, "spellFX_SummonCreature_ORIGIN"),
    ])
    {
        Look = new GothicOnline.Api.PlayerVisual("hum_body_Naked0", 1, "Hum_Head_Psionic", 13, 0, 0),
        Overlays = [],
        Rune = GameItems.ITRU_INSTANTFIREBALL,
        MagicCircle = 6,
        GameMana = 999999,
        SkillMana = 600,
    };

    public static readonly HeroDef Cipher = new("cipher", "Cipher", "Pijak z Bagien", "Wojownik",
        "Łowca smoków z bagiennego obozu. Hałaśliwy, wiecznie zalany, a jednak najtwardszy w całej gospodzie: piwo go leczy, beczką ogłusza, a kiedy ma dość — zmienia się w cieniostwora.",
        0, 820, 100, 60, HeroWeapon.OneHanded, GameItems.ITMW_1H_SLD_AXE, GameItems.ITAR_DJG_H, null,
    [
        A("piwosz", "Piwosz",
            "Pasywna: poza walką (nikogo nie bijesz i nikt nie bije ciebie) co ok. 50 s (losowo, nigdy mniej) wypijasz piwo i odzyskujesz 20% zdrowia.",
            AbilityKind.Brewer, 0, 0, 30, 0, 20, 50, ""),
        A("beczka", "Beczka",
            "Włącza celowanie: strzałkami wskazujesz miejsce, lewy przycisk myszy albo Enter rzuca beczką, Esc anuluje. Po upadku ogłusza wrogów w obszarze na 2 s.",
            AbilityKind.Barrel, 12, 1200, 300, 0, 0, 2, "spellFX_CipherBeerBurst"),
        A("mocnaglowa", "Mocna głowa",
            "Przez 3 s jesteś odporny na ogłuszenia i trucizny, a te, które już na tobie są, znikają.",
            AbilityKind.Ironhead, 18, 0, 0, 0, 0, 3, "spellFX_PalLight"),
        A("cieniostwor", "Cieniostwór",
            "Zmieniasz się w cieniostwora na 10 s: atakujesz o 60% szybciej, a cios zadaje tyle co zwykle +15%.",
            AbilityKind.ShadowForm, 50, 0, 0, 60, 15, 10, "spellFX_Fear_Ground"),
    ])
    {
        Look = new GothicOnline.Api.PlayerVisual("hum_body_Naked0", 1, "Hum_Head_Bald", 95, 0, 0),
        Overlays = [],
        SkillMana = 420,
    };

    public static readonly HeroDef Thorus = new("thorus", "Thorus", "Strażnik Bramy", "Wojownik",
        "Dowódca Strażników Starego Obozu w pancerzu ze szczęką rekina. Jego ciosy powodują krwawienie (do 5 stacków). Kręci mieczem w kółko, wysysa życie, przyciąga wrogów do siebie i dobija z góry.",
        0, 950, 110, 60, HeroWeapon.TwoHanded, GameItems.ITMW_2H_SLD_SWORD, GameItems.ITAR_THORUS_ADDON, null,
    [
        A("obrot", "Obrót",
            "Wymaga dobytej broni. Kręcisz mieczem dookoła: na krawędzi zasięgu (ostrze) pełne obrażenia, bliżej (drzewiec) tylko 35% i bez leczenia. Ostrze nakłada krwawienie i leczy część brakującego zdrowia: 17% / 34% / 51% za 1 / 2 / 3+ trafionych bohaterów albo potworów.",
            AbilityKind.Spin, 8, 0, 300, 70, 0, 1.12f, "spellFX_ThorusBlade"),
        A("zadzakrwi", "Żądza krwi",
            "Przez 1 s następny cios w wrogiego gracza zabiera mu trochę zdrowia i oddaje je tobie.",
            AbilityKind.Bloodlust, 12, 0, 0, 45, 0, 1, "spellFX_ThorusBlood"),
        A("przyciagniecie", "Przyciągnięcie",
            "Zagarniasz do siebie moby i bohaterów wroga w promieniu 6,5 m: lądują tuż przed tobą.",
            AbilityKind.Pull, 16, 0, 650, 0, 0, 0.5f, ""),
        A("egzekucja", "Egzekucja",
            "Skaczesz na najbliższego wroga (bohaterowie mają pierwszeństwo) i tniesz z góry czerwonym mieczem. Zabicie bohatera zwraca manę i odnawia tę zdolność.",
            AbilityKind.Execute, 20, 700, 0, 170, 0, HeroBuffs.ExecuteSeconds, "spellFX_ThorusBlade"),
    ])
    {
        Look = new GothicOnline.Api.PlayerVisual("hum_body_Naked0", 3, "Hum_Head_Fighter", 4, 0, 0),
        Overlays = ["HumanS_Thorus_V5.mds"],
        Bleeds = true,
        SkillMana = 380,
    };

    public static readonly HeroDef Raven = new("kruk", "Kruk", "Wybraniec Beliara", "Wojownik",
        "Były magnat Starego Obozu, który w Górniczej Dolinie oddał się Beliarowi. Jego czarny miecz zabija i zniewala: kto padnie od ostrza, po chwili walczy u jego boku. Kruk ogłusza językiem Beliara, zachodzi wroga od tyłu, a z góry spuszcza na pole bitwy wielki miecz swego pana.",
        0, 900, 110, 60, HeroWeapon.TwoHanded, GameItems.ITMW_BELIARWEAPON_RAVEN, GameItems.ITAR_RAVEN_ADDON, null,
    [
        A("jezyk", "Język Beliara",
            "Na ziemi 6 m przed tobą pojawia się czarna plama. Po 1 s każdy wróg w środku (potwór albo bohater) zostaje ogłuszony na 1,5 s.",
            AbilityKind.BeliarTongue, 15, 600, 300, 0, 0, 1, "spellFX_RavenTongue") with { StunSeconds = 1.5f },
        A("sluga", "Sługa",
            "Pasywna: gdy zabijesz bohatera wrogiej drużyny, jego kopia walczy po twojej stronie przez 3,5 s (tylko zwykłe ciosy).",
            AbilityKind.Servant, 0, 0, 0, 25, 450, 3.5f, "spellFX_Fear_Ground"),
        A("okrazenie", "Okrążenie",
            "Jeśli wróg jest blisko (8 m), pojawiasz się tuż za nim. Bohaterowie mają pierwszeństwo przed potworami.",
            AbilityKind.Flank, 18, 800, 0, 0, 0, 0, "spellFX_Teleport_CAST"),
        A("silabeliara", "Siła Beliara",
            "Wyskakujesz w powietrze i wskazujesz miejsce lądowania (strzałki, lewy przycisk myszy albo Enter; Esc anuluje). Tam spada wielki miecz Beliara, a ty lądujesz obok: wszyscy wokół (potwory i bohaterowie) dostają obrażenia.",
            AbilityKind.BeliarMight, 150, 1100, 450, 220, 0, HeroBuffs.ExecuteSeconds, "spellFX_RavenImpact") with { Magic = true },
    ])
    {
        Look = new GothicOnline.Api.PlayerVisual("hum_body_Naked0", 1, "Hum_Head_Thief", 2, 0, 0),
        Overlays = ["HumanS_Thorus_V5.mds"],
        WeaponMana = 666666,
        SkillMana = 500,
    };

    public static readonly HeroDef Urshak = new("urshak", "Ur-Shak", "Szaman Plemienia", "Mag",
        "Orkowy szaman, który nie służy żadnemu wodzowi, tylko plemieniu. Płonie i podpala wrogów, stawia plemienny posąg, który strzela w ich szeregi, dzieli się doświadczeniem poległych z sojusznikami, a w potrzebie osłania całe plemię przed każdym ciosem.",
        0, 800, 90, 50, HeroWeapon.None, null, null, null,
    [
        A("wielkakula", "Wielka Kula",
            "Zaczynasz płonąć na 10 s: Twoje następne 3 zwykłe ciosy podpalają trafionego na 3 s (obrażenia magiczne co sekundę).",
            AbilityKind.Ignite, 9, 0, 0, 18, 0, 10, HeroBuffs.IgniteSelfFx),
        A("postep", "Postęp Plemienia",
            "Zabity wróg (mob) daje 1 stack, wrogi bohater 5 (do 40, giną ze śmiercią). Użyta na sojuszniku, na którym masz focus (do 7 m), zużywa wszystkie stacki: sojusznik dostaje XP, jakie dałyby te moby, " +
            "ale tylko 10% (+5% za każde 100 AP, najwyżej 35%).",
            AbilityKind.TribeProgress, 15, 700, 0, 10, 0, 0, "spellFX_PalHeal"),
        A("totem", "Plemienny Posąg",
            "Stawia przed tobą totem, który przez 10 s co sekundę strzela ognistym pociskiem w najbliższego wroga w zasięgu 9 m (bohaterowie mają pierwszeństwo).",
            AbilityKind.Totem, 22, 900, 0, 22, 0, 10, "spellFX_Fear_Ground"),
        A("plemie", "Plemię jest najważniejsze",
            "Ty i sojusznicy w promieniu 8 m jesteście przez 2 s (do 3 s: stopnie i AP) całkowicie niewrażliwi na obrażenia; nad głową każdego z was unosi się orkowa czaszka.",
            AbilityKind.TribeGuard, 90, 0, 800, 0, 0, 2, "spellFX_PalLight"),
    ])
    {
        Instance = GameNpcs.NONE_110_URSHAK,
        Rune = GameItems.ITRU_PYROKINESIS,
        MagicCircle = 6,
        GameMana = 999999,
        SkillMana = 450,
    };

    public static readonly IReadOnlyList<HeroDef> All = [Lee, Diego, Xardas, Cipher, Thorus, Raven, Urshak, Knight, Hunter, Mage, Assassin, Priest, Berserker];

    public static HeroDef? Find(string? key) => All.FirstOrDefault(h => string.Equals(h.Key, key, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<HeroDef> Free => All.Where(h => h.Price == 0);
}
