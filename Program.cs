using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;

namespace UltimateDrawMiner
{
    public class MatchData
    {
        public string Season { get; set; }
        public int MatchWeek { get; set; }
        public string League { get; set; }
        public DateTime MatchDate { get; set; }
        public string MatchTime { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public string HTR { get; set; }
        public string FTR { get; set; }
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }

        public double H { get; set; }
        public double D { get; set; }
        public double A { get; set; }

        public double CH { get; set; }
        public double CD { get; set; }
        public double CA { get; set; }

        public bool Is10 => HTR == "H" && FTR == "D";
        public bool Is20 => HTR == "A" && FTR == "D";
        public bool IsDrawReversal => Is10 || Is20;
        public string DrawRevType => Is10 ? "1/0" : (Is20 ? "2/0" : "");

        public string OpenOddsStr => $"{H:0.00}-{D:0.00}-{A:0.00}";
        public string CloseOddsStr => $"{CH:0.00}-{CD:0.00}-{CA:0.00}";
    }

    public class PredictionResult
    {
        public MatchData Match { get; set; }
        public double Score { get; set; }
        public string OddsStr { get; set; }
        public List<string> Tags { get; set; }

        public bool IsVip => Tags.Any(t => t.Contains("VIP"));
        public bool IsTeamSpecific => Tags.Any(t => t.Contains("TAKIMA ÖZEL"));
        public bool IsNewSeasonGolden => Tags.Any(t => t.Contains("YENİ SEZON TRENDİ"));
        public bool IsH2HDeepLoop => Tags.Any(t => t.Contains("ZİNCİR DÖNGÜSÜ"));

        public int PriorityLevel => IsVip ? 6 : (IsTeamSpecific ? 5 : (IsNewSeasonGolden ? 4 : (IsH2HDeepLoop ? 3 : 0)));
    }

    class Program
    {
        static readonly string[] Leagues = { "E0", "E1", "E2", "E3", "EC", "D1", "D2", "SP1", "SP2", "I1", "I2", "F1", "F2", "T1", "N1", "B1", "P1", "G1", "SC0", "SC1", "SC2", "SC3" };
        static readonly int YearsBack = 10;
        public static string CurrentSeasonCode = "";
        public static string PrevSeasonCode = "";

        // VIP VERİTABANI
        static readonly Dictionary<string, int> VipScoreCombos = new Dictionary<string, int>
        {
            {"0-1 & 1-1", 152}, {"1-1 & 1-2", 150}, {"1-0 & 1-1", 140}, {"0-0 & 1-1", 133},
            {"1-1 & 2-1", 119}, {"1-1 & 2-0", 109}, {"1-0 & 1-2", 108}, {"0-1 & 1-0", 107},
            {"1-1 & 2-2", 103}, {"1-1 & 1-1", 97}, {"0-0 & 1-0", 97}, {"1-2 & 2-1", 95},
            {"0-1 & 2-1", 94}, {"0-2 & 1-1", 92}, {"0-1 & 1-2", 86}, {"0-0 & 0-1", 82}
        };

        static readonly Dictionary<string, int> VipOpenOdds = new Dictionary<string, int>
        {
            {"2.00-3.50-3.60", 21}, {"2.00-3.40-3.75", 19}, {"2.20-3.30-3.40", 18},
            {"2.20-3.50-3.10", 17}, {"2.20-3.40-3.20", 15}, {"1.95-3.50-4.00", 15},
            {"2.15-3.40-3.40", 15}, {"2.10-3.40-3.50", 15}, {"2.30-3.40-3.00", 14},
            {"2.20-3.10-3.50", 14}, {"2.30-3.40-3.10", 14}, {"2.40-3.30-3.00", 14},
            {"1.80-3.50-4.50", 13}, {"2.05-3.50-3.60", 13}, {"2.25-3.40-3.20", 13}
        };

        static readonly Dictionary<string, int> VipCloseOdds = new Dictionary<string, int>
        {
            {"2.05-3.40-3.60", 16}, {"1.75-3.60-4.75", 16}, {"2.05-3.50-3.60", 14},
            {"2.10-3.40-3.60", 12}, {"2.15-3.30-3.50", 12}, {"1.80-3.60-4.50", 12},
            {"2.00-3.40-3.80", 11}, {"1.57-4.00-6.00", 11}, {"2.45-3.00-3.20", 11},
            {"1.60-3.80-6.00", 10}, {"2.45-3.20-3.00", 10}, {"2.15-3.40-3.40", 10}
        };

        static Dictionary<string, List<MatchData>> teamTimelines = new Dictionary<string, List<MatchData>>();
        static Dictionary<string, List<MatchData>> h2hTimelines = new Dictionary<string, List<MatchData>>();

        static List<MatchData> CurrentSeasonDrawRevs = new List<MatchData>();
        static Dictionary<string, List<MatchData>> CurrentSeasonDrawRevsByOdds = new Dictionary<string, List<MatchData>>();

        static async Task Main(string[] args)
        {
            int currentYear = DateTime.Now.Month > 7 ? DateTime.Now.Year : DateTime.Now.Year - 1;
            CurrentSeasonCode = $"{currentYear.ToString().Substring(2)}{(currentYear + 1).ToString().Substring(2)}";
            PrevSeasonCode = $"{(currentYear - 1).ToString().Substring(2)}{(currentYear).ToString().Substring(2)}";

            Console.Title = "Ultimate Miner V48: TEAM-SPECIFIC MEMORY (Takım Bazlı Hafıza)";
            Console.WriteLine("Veritabanı Yükleniyor... (VIP ve Takıma Özel Hafıza Aktif)");

            var history = await LoadHistoricalData();
            Console.WriteLine($"\nVeritabanı Yüklendi: {history.Count} maç. Öğrenme aşaması başladı...\n");

            BuildCoreStats(history);
            MineAndExportCurrentSeason(history);

            Console.WriteLine("\nGüncel Fikstür Çekiliyor...\n");
            var fixtures = await LoadFixtures();

            if (fixtures.Count > 0) MakeAdvancedPredictions(history, fixtures);
            else Console.WriteLine("Fikstür bulunamadı.");

            Console.WriteLine("\nİşlem bitti. Çıkış için bir tuşa basın.");
            Console.ReadKey();
        }

        static void MineAndExportCurrentSeason(List<MatchData> history)
        {
            CurrentSeasonDrawRevs = history.Where(m => m.Season == CurrentSeasonCode && m.IsDrawReversal).OrderByDescending(m => m.MatchDate).ToList();

            foreach (var m in CurrentSeasonDrawRevs)
            {
                if (m.H > 0 && m.D > 0 && m.A > 0)
                {
                    string key = m.OpenOddsStr;
                    if (!CurrentSeasonDrawRevsByOdds.ContainsKey(key)) CurrentSeasonDrawRevsByOdds[key] = new List<MatchData>();
                    CurrentSeasonDrawRevsByOdds[key].Add(m);
                }
            }

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string excelFilePath = Path.Combine(desktopPath, "Yeni_Sezon_1den0_2den0_Verileri.csv");

            var csvLines = new List<string>
            {
                "Tarih;Lig;Hafta;Ev Sahibi;Deplasman;İY-MS;Tur;Acilis 1;Acilis X;Acilis 2;Kapanis 1;Kapanis X;Kapanis 2;Ev Sahibi Gecen Hafta;Deplasman Gecen Hafta"
            };

            foreach (var m in CurrentSeasonDrawRevs)
            {
                var hPrev = GetLastMatchBefore(m.HomeTeam, m.MatchDate);
                var aPrev = GetLastMatchBefore(m.AwayTeam, m.MatchDate);
                string hScore = hPrev != null ? GetTeamFormScore(hPrev, m.HomeTeam) : "Yok";
                string aScore = aPrev != null ? GetTeamFormScore(aPrev, m.AwayTeam) : "Yok";

                string line = $"{m.MatchDate:dd.MM.yyyy};{m.League};{m.MatchWeek};{m.HomeTeam};{m.AwayTeam};{m.HTR}-{m.FTR};{m.DrawRevType};" +
                              $"{m.H:0.00};{m.D:0.00};{m.A:0.00};{m.CH:0.00};{m.CD:0.00};{m.CA:0.00};{hScore};{aScore}";
                csvLines.Add(line);
            }

            try { File.WriteAllLines(excelFilePath, csvLines, Encoding.UTF8); } catch { }
        }

        static MatchData GetLastMatchBefore(string team, DateTime date)
        {
            if (!teamTimelines.ContainsKey(team)) return null;
            return teamTimelines[team].LastOrDefault(m => m.MatchDate < date);
        }

        static string GetTeamFormScore(MatchData m, string teamName)
        {
            return m.HomeTeam == teamName ? $"{m.HomeGoals}-{m.AwayGoals}" : $"{m.AwayGoals}-{m.HomeGoals}";
        }

        static void BuildCoreStats(List<MatchData> history)
        {
            foreach (var match in history.OrderBy(m => m.MatchDate))
            {
                if (!teamTimelines.ContainsKey(match.HomeTeam)) teamTimelines[match.HomeTeam] = new List<MatchData>();
                if (!teamTimelines.ContainsKey(match.AwayTeam)) teamTimelines[match.AwayTeam] = new List<MatchData>();

                teamTimelines[match.HomeTeam].Add(match);
                teamTimelines[match.AwayTeam].Add(match);

                string[] pair = { match.HomeTeam, match.AwayTeam };
                Array.Sort(pair);
                string h2hKey = $"{pair[0]} - {pair[1]}";

                if (!h2hTimelines.ContainsKey(h2hKey)) h2hTimelines[h2hKey] = new List<MatchData>();
                h2hTimelines[h2hKey].Add(match);
            }
        }

        static void MakeAdvancedPredictions(List<MatchData> history, List<MatchData> fixtures)
        {
            var predictions = new List<PredictionResult>();

            foreach (var match in fixtures.Where(f => f.H > 0 && f.A > 0))
            {
                double baseProb = 0.05;
                double yusufSpecialWeight = 1.0;
                List<string> specialTags = new List<string>();

                string openKey = match.OpenOddsStr;
                string closeKey = match.CloseOddsStr;

                // 1. VIP KONTROLLER (Senin Veritabanın)
                if (VipOpenOdds.ContainsKey(openKey))
                {
                    yusufSpecialWeight *= 2.5;
                    specialTags.Add($"🏆 [VIP AÇILIŞ ORANI: {openKey} oranı geçmişte tam {VipOpenOdds[openKey]} KEZ 1/0 veya 2/0 getirdi!]");
                }
                if (match.CH > 0 && VipCloseOdds.ContainsKey(closeKey))
                {
                    yusufSpecialWeight *= 2.5;
                    specialTags.Add($"🏆 [VIP KAPANIŞ ORANI: {closeKey} oranı geçmişte tam {VipCloseOdds[closeKey]} KEZ 1/0 veya 2/0 getirdi!]");
                }

                // 2. YENİ SEZON TREND TESPİTİ
                if (CurrentSeasonDrawRevsByOdds.ContainsKey(openKey))
                {
                    var hitMatches = CurrentSeasonDrawRevsByOdds[openKey];
                    if (hitMatches.Count > 1)
                    {
                        yusufSpecialWeight *= 2.5;
                        specialTags.Add($"🌟 [YENİ SEZON TRENDİ: Bu oran ({openKey}) yeni sezonda tam {hitMatches.Count} KEZ 1/0 veya 2/0 getirdi!]");
                    }
                }

                // --- 3. YENİ: TAKIMA ÖZEL ORAN VE SKOR HAFIZASI ---
                string pairKey = "";
                var hPrev = GetLastMatchBefore(match.HomeTeam, match.MatchDate);
                var aPrev = GetLastMatchBefore(match.AwayTeam, match.MatchDate);

                if (hPrev != null && aPrev != null)
                {
                    string hScore = GetTeamFormScore(hPrev, match.HomeTeam);
                    string aScore = GetTeamFormScore(aPrev, match.AwayTeam);

                    string[] scorePair = { hScore, aScore };
                    Array.Sort(scorePair);
                    pairKey = $"{scorePair[0]} & {scorePair[1]}";

                    if (VipScoreCombos.ContainsKey(pairKey))
                    {
                        yusufSpecialWeight *= 2.0;
                        specialTags.Add($"🥇 [VIP SKOR KOMBİNASYONU: Takımların geçen haftaki skorları ({pairKey}) geçmişte tam {VipScoreCombos[pairKey]} KEZ 1/0 veya 2/0 getirdi!]");
                    }
                }

                foreach (var team in new[] { match.HomeTeam, match.AwayTeam })
                {
                    if (teamTimelines.ContainsKey(team))
                    {
                        // A) Takıma Özel Oran Taraması
                        var pastOddsHits = teamTimelines[team].Where(m => m.OpenOddsStr == openKey && m.IsDrawReversal).ToList();
                        if (pastOddsHits.Any())
                        {
                            yusufSpecialWeight *= (2.0 + (pastOddsHits.Count * 0.5));
                            specialTags.Add($"🎯 [TAKIMA ÖZEL ORAN: {team}, bu maçtaki açılış oranıyla ({openKey}) daha önce {pastOddsHits.Count} kez 1/0 veya 2/0 getirdi!]");
                            foreach (var hit in pastOddsHits)
                            {
                                string opp = hit.HomeTeam == team ? hit.AwayTeam : hit.HomeTeam;
                                specialTags.Add($"     👉 Tarih: {hit.MatchDate:dd.MM.yyyy} | Rakip: {opp} | Sonuç: {hit.DrawRevType} ({hit.HomeTeam} {hit.HomeGoals}-{hit.AwayGoals} {hit.AwayTeam})");
                            }
                        }

                        // B) Takıma Özel Skor Kombinasyonu Taraması
                        if (!string.IsNullOrEmpty(pairKey))
                        {
                            var pastComboHits = new List<MatchData>();
                            foreach (var pm in teamTimelines[team].Where(m => m.IsDrawReversal))
                            {
                                var pmHPrev = GetLastMatchBefore(pm.HomeTeam, pm.MatchDate);
                                var pmAPrev = GetLastMatchBefore(pm.AwayTeam, pm.MatchDate);
                                if (pmHPrev != null && pmAPrev != null)
                                {
                                    string pmHScore = GetTeamFormScore(pmHPrev, pm.HomeTeam);
                                    string pmAScore = GetTeamFormScore(pmAPrev, pm.AwayTeam);
                                    string[] pmPair = { pmHScore, pmAScore };
                                    Array.Sort(pmPair);
                                    if ($"{pmPair[0]} & {pmPair[1]}" == pairKey)
                                    {
                                        pastComboHits.Add(pm);
                                    }
                                }
                            }

                            if (pastComboHits.Any())
                            {
                                yusufSpecialWeight *= (2.0 + (pastComboHits.Count * 0.5));
                                specialTags.Add($"🧠 [TAKIMA ÖZEL SKOR HAFIZASI: {team}, bugünkü skor kombosuyla ({pairKey}) daha önce {pastComboHits.Count} kez 1/0 veya 2/0 getirdi!]");
                                foreach (var hit in pastComboHits)
                                {
                                    string opp = hit.HomeTeam == team ? hit.AwayTeam : hit.HomeTeam;
                                    specialTags.Add($"     👉 Tarih: {hit.MatchDate:dd.MM.yyyy} | Rakip: {opp} | Sonuç: {hit.DrawRevType} ({hit.HomeTeam} {hit.HomeGoals}-{hit.AwayGoals} {hit.AwayTeam})");
                                }
                            }
                        }
                    }
                }

                // 4. H2H DERİN ZİNCİR TARAMASI
                string[] pairH2H = { match.HomeTeam, match.AwayTeam };
                Array.Sort(pairH2H);
                string h2hKey = $"{pairH2H[0]} - {pairH2H[1]}";

                if (h2hTimelines.ContainsKey(h2hKey))
                {
                    var h2hMatches = h2hTimelines[h2hKey].OrderBy(m => m.MatchDate).ToList();
                    if (h2hMatches.Count >= 3)
                    {
                        int last = h2hMatches.Count - 1;
                        int maxChainFound = 0;

                        for (int chainLength = 4; chainLength >= 2; chainLength--)
                        {
                            if (h2hMatches.Count < chainLength + 1) continue;
                            if (maxChainFound > 0) break;

                            List<string> currentChainScores = new List<string>();
                            for (int i = last - chainLength + 1; i <= last; i++)
                                currentChainScores.Add($"{h2hMatches[i].HomeGoals}-{h2hMatches[i].AwayGoals}");

                            int loopHits = 0; int loopTotal = 0;
                            for (int i = 0; i <= h2hMatches.Count - chainLength - 1; i++)
                            {
                                bool isMatch = true;
                                for (int j = 0; j < chainLength; j++)
                                {
                                    string historicalScore = $"{h2hMatches[i + j].HomeGoals}-{h2hMatches[i + j].AwayGoals}";
                                    if (historicalScore != currentChainScores[j]) { isMatch = false; break; }
                                }
                                if (isMatch)
                                {
                                    loopTotal++;
                                    if (h2hMatches[i + chainLength].IsDrawReversal) loopHits++;
                                }
                            }

                            if (loopTotal > 0 && loopHits > 0)
                            {
                                maxChainFound = chainLength;
                                yusufSpecialWeight *= (chainLength * 1.5);
                                string currentChainDisplay = string.Join(" ➡ ", currentChainScores);
                                specialTags.Add($"🔗 [DERİN ZİNCİR DÖNGÜSÜ]: Aralarındaki son {chainLength} maç ({currentChainDisplay}) bitti. Sonraki maç {loopHits} KEZ 1/0 veya 2/0 gelmiş!");
                            }
                        }
                    }
                }

                double finalScore = baseProb * yusufSpecialWeight * 100;
                var distinctTags = specialTags.Distinct().ToList();

                // En az 1 güçlü veri varsa listeye al
                if (distinctTags.Count > 0)
                {
                    predictions.Add(new PredictionResult
                    {
                        Match = match,
                        Score = finalScore,
                        OddsStr = match.OpenOddsStr,
                        Tags = distinctTags
                    });
                }
            }

            var sortedPreds = predictions.OrderByDescending(x => x.PriorityLevel).ThenByDescending(x => x.Score).Take(40).ToList();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n=========================================================================================================================");
            Console.WriteLine("                  V48: TEAM-SPECIFIC MEMORY (Takımların Kendi Şifrelerini ve Geçmişlerini Tarayan Modül)");
            Console.WriteLine("=========================================================================================================================\n");
            Console.ResetColor();

            if (sortedPreds.Count == 0)
            {
                Console.WriteLine("Bültende bu filtrelere uyan hiçbir maç bulunamadı.");
                return;
            }

            foreach (var p in sortedPreds)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                string timeStr = string.IsNullOrEmpty(p.Match.MatchTime) ? "Saat Yok" : p.Match.MatchTime;
                Console.WriteLine($"[LİG: {p.Match.League}] | {p.Match.MatchDate:dd.MM.yyyy} - TSİ {timeStr} | {p.Match.HomeTeam} - {p.Match.AwayTeam}");
                Console.Write($"Oranlar: {p.OddsStr,-15} | Güven Puanı: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"%{p.Score:0.00}");
                Console.ResetColor();

                Console.WriteLine("Tespit Edilen Somut Kanıtlar:");
                foreach (var tag in p.Tags)
                {
                    if (tag.Contains("🎯 [TAKIMA ÖZEL ORAN")) { Console.BackgroundColor = ConsoleColor.DarkBlue; Console.ForegroundColor = ConsoleColor.White; }
                    else if (tag.Contains("🧠 [TAKIMA ÖZEL SKOR")) { Console.BackgroundColor = ConsoleColor.DarkCyan; Console.ForegroundColor = ConsoleColor.Black; }
                    else if (tag.Contains("👉 Tarih:")) { Console.ForegroundColor = ConsoleColor.Cyan; }
                    else if (tag.Contains("🏆 [VIP")) { Console.BackgroundColor = ConsoleColor.DarkMagenta; Console.ForegroundColor = ConsoleColor.White; }
                    else if (tag.Contains("🥇 [VIP SKOR")) { Console.BackgroundColor = ConsoleColor.DarkGreen; Console.ForegroundColor = ConsoleColor.White; }
                    else if (tag.Contains("🌟 [YENİ SEZON TRENDİ")) { Console.BackgroundColor = ConsoleColor.DarkYellow; Console.ForegroundColor = ConsoleColor.Black; }
                    else if (tag.Contains("🔗 [DERİN ZİNCİR")) { Console.ForegroundColor = ConsoleColor.Magenta; }
                    else Console.ForegroundColor = ConsoleColor.Gray;

                    Console.WriteLine("   " + tag);
                    Console.ResetColor();
                }
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------");
            }
        }

        static async Task<List<MatchData>> LoadHistoricalData()
        {
            var list = new List<MatchData>();
            string localFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FootballDataCache");
            if (!Directory.Exists(localFolder)) Directory.CreateDirectory(localFolder);

            using (var client = new HttpClient())
            {
                int currentYear = DateTime.Now.Month > 7 ? DateTime.Now.Year : DateTime.Now.Year - 1;
                for (int i = 0; i <= YearsBack; i++)
                {
                    string seasonCode = $"{(currentYear - i).ToString().Substring(2)}{(currentYear - i + 1).ToString().Substring(2)}";
                    bool isCurrent = (seasonCode == CurrentSeasonCode);
                    foreach (var league in Leagues)
                    {
                        string localPath = Path.Combine(localFolder, $"{seasonCode}_{league}.csv");
                        try
                        {
                            string csvData = (!isCurrent && File.Exists(localPath)) ? await File.ReadAllTextAsync(localPath, Encoding.UTF8) : await client.GetStringAsync($"https://www.football-data.co.uk/mmz4281/{seasonCode}/{league}.csv");
                            if (!isCurrent && !File.Exists(localPath)) await File.WriteAllTextAsync(localPath, csvData, Encoding.UTF8);
                            if (!string.IsNullOrWhiteSpace(csvData)) list.AddRange(ParseCsv(csvData, true, league, seasonCode));
                        }
                        catch { }
                    }
                    Console.Write(".");
                }
            }
            return list;
        }

        static async Task<List<MatchData>> LoadFixtures()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var data = await client.GetStringAsync("https://www.football-data.co.uk/fixtures.csv");
                    return ParseCsv(data, false, "Fix", "Current");
                }
                catch { return new List<MatchData>(); }
            }
        }

        static List<MatchData> ParseCsv(string csvData, bool isHistory, string defaultLeague, string seasonCode)
        {
            var matches = new List<MatchData>();
            using (var reader = new StringReader(csvData))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine)) return matches;

                var headers = headerLine.Split(',').Select(h => h.Trim()).ToList();
                int iHome = headers.IndexOf("HomeTeam"), iAway = headers.IndexOf("AwayTeam");
                int iDate = headers.IndexOf("Date"), iTime = headers.IndexOf("Time");
                int iHTR = headers.IndexOf("HTR"), iFTR = headers.IndexOf("FTR");
                int iFTHG = headers.IndexOf("FTHG"), iFTAG = headers.IndexOf("FTAG");

                int iH = headers.FindIndex(x => x == "B365H" || x == "AvgH");
                int iD = headers.FindIndex(x => x == "B365D" || x == "AvgD");
                int iA = headers.FindIndex(x => x == "B365A" || x == "AvgA");

                int iCH = headers.IndexOf("B365CH");
                int iCD = headers.IndexOf("B365CD");
                int iCA = headers.IndexOf("B365CA");

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var cols = line.Split(',');
                    if (cols.Length <= iA || iHome == -1 || iA == -1) continue;

                    try
                    {
                        var m = new MatchData { League = defaultLeague, HomeTeam = cols[iHome].Trim(), AwayTeam = cols[iAway].Trim(), Season = seasonCode };
                        if (iDate != -1 && cols.Length > iDate)
                        {
                            if (DateTime.TryParseExact(cols[iDate].Trim(), new[] { "dd/MM/yyyy", "dd/MM/yy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)) m.MatchDate = dt;
                            else m.MatchDate = DateTime.Now;
                        }
                        if (iTime != -1 && cols.Length > iTime && !string.IsNullOrWhiteSpace(cols[iTime]))
                        {
                            string rawTime = cols[iTime].Trim();
                            if (TimeSpan.TryParse(rawTime, out TimeSpan timeOfDay))
                            {
                                DateTime matchDateTimeUk = m.MatchDate.Add(timeOfDay);
                                try
                                {
                                    m.MatchDate = TimeZoneInfo.ConvertTime(matchDateTimeUk, TimeZoneInfo.FindSystemTimeZoneById(Environment.OSVersion.Platform == PlatformID.Win32NT ? "GMT Standard Time" : "Europe/London"), TimeZoneInfo.FindSystemTimeZoneById(Environment.OSVersion.Platform == PlatformID.Win32NT ? "Turkey Standard Time" : "Europe/Istanbul")).Date;
                                    m.MatchTime = TimeZoneInfo.ConvertTime(matchDateTimeUk, TimeZoneInfo.FindSystemTimeZoneById(Environment.OSVersion.Platform == PlatformID.Win32NT ? "GMT Standard Time" : "Europe/London"), TimeZoneInfo.FindSystemTimeZoneById(Environment.OSVersion.Platform == PlatformID.Win32NT ? "Turkey Standard Time" : "Europe/Istanbul")).ToString("HH:mm");
                                }
                                catch { m.MatchTime = matchDateTimeUk.AddHours(3).ToString("HH:mm"); m.MatchDate = matchDateTimeUk.AddHours(3).Date; }
                            }
                            else m.MatchTime = rawTime;
                        }

                        if (double.TryParse(cols[iH], NumberStyles.Any, CultureInfo.InvariantCulture, out double h)) m.H = h;
                        if (double.TryParse(cols[iD], NumberStyles.Any, CultureInfo.InvariantCulture, out double d)) m.D = d;
                        if (double.TryParse(cols[iA], NumberStyles.Any, CultureInfo.InvariantCulture, out double a)) m.A = a;

                        if (iCH != -1 && cols.Length > iCA)
                        {
                            if (double.TryParse(cols[iCH], NumberStyles.Any, CultureInfo.InvariantCulture, out double ch)) m.CH = ch;
                            if (double.TryParse(cols[iCD], NumberStyles.Any, CultureInfo.InvariantCulture, out double cd)) m.CD = cd;
                            if (double.TryParse(cols[iCA], NumberStyles.Any, CultureInfo.InvariantCulture, out double ca)) m.CA = ca;
                        }

                        if (isHistory && iHTR != -1 && iFTR != -1 && iFTHG != -1 && iFTAG != -1)
                        {
                            m.HTR = cols[iHTR]; m.FTR = cols[iFTR];
                            if (int.TryParse(cols[iFTHG], out int hg)) m.HomeGoals = hg;
                            if (int.TryParse(cols[iFTAG], out int ag)) m.AwayGoals = ag;
                        }
                        matches.Add(m);
                    }
                    catch { }
                }
            }
            return matches;
        }
    }
}