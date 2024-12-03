using SiraUtil.Affinity;
using System;
using System.Threading;
using TMPro;
using UnityEngine;

namespace ScoreAcc
{
    class LevelStatsViewPatches : IAffinity
    {
        private readonly BeatmapLevelLoader _beatmapLevelLoader;
        private readonly StandardLevelDetailViewController _standardLevelDetailViewController;
        private readonly BeatmapLevelsEntitlementModel _beatmapLevelsEntitlementModel;
        private readonly BeatmapDataLoader _beatmapDataLoader = new BeatmapDataLoader();

        public LevelStatsViewPatches(BeatmapLevelLoader beatmapLevelLoader, StandardLevelDetailViewController standardLevelDetailViewController, BeatmapLevelsEntitlementModel beatmapLevelsEntitlementModel)
        {
            _beatmapLevelLoader = beatmapLevelLoader;
            _standardLevelDetailViewController = standardLevelDetailViewController;
            _beatmapLevelsEntitlementModel = beatmapLevelsEntitlementModel;
        }

        [AffinityPatch(typeof(LevelStatsView), nameof(LevelStatsView.ShowStats))]
        [AffinityPostfix]
        private void Postfix(in BeatmapKey beatmapKey, PlayerData playerData, TextMeshProUGUI ____highScoreText)
        {
            if (playerData == null) return;

            PlayerLevelStatsData playerLevelStatsData = playerData.GetOrCreatePlayerLevelStatsData(beatmapKey);

            if (playerLevelStatsData.validScore)
            {
                var currentScore = playerLevelStatsData.highScore;
                if (currentScore != 0)
                {
                    ShowPercentage(beatmapKey, playerData, currentScore, ____highScoreText);
                }
            }
        }

        private async void ShowPercentage(BeatmapKey beatmapKey, PlayerData playerData, int currentScore,TextMeshProUGUI ____highScoreText)
        {
            BeatmapLevel beatmapLevel = _standardLevelDetailViewController.beatmapLevel;
            BeatmapLevelDataVersion beatmapLevelDataVersion = await _beatmapLevelsEntitlementModel.GetLevelDataVersionAsync(beatmapLevel.levelID, CancellationToken.None);
            LoadBeatmapLevelDataResult beatmapLevelData = await _beatmapLevelLoader.LoadBeatmapLevelDataAsync(beatmapLevel, beatmapLevelDataVersion, CancellationToken.None);
            IReadonlyBeatmapData currentReadonlyBeatmapData = await _beatmapDataLoader.LoadBeatmapDataAsync(beatmapLevelData.beatmapLevelData, beatmapKey, beatmapLevel.beatsPerMinute, true, null, null, beatmapLevelDataVersion, playerData.gameplayModifiers, playerData.playerSpecificSettings, false);

            int maxScore = ScoreModel.ComputeMaxMultipliedScoreForBeatmap(currentReadonlyBeatmapData);

            if (maxScore != 0)
            {
                double currentPercentage = ScoreCalc.CalculatePercentage(maxScore, currentScore);
                ____highScoreText.text = $"{currentScore} ({currentPercentage:F2}%)";
            }
        }
    }

    class ResultsViewData
    {
        public static int highScore = 0;

        public ResultsViewData(PlayerDataModel playerDataModel, BeatmapKey beatmapKey)
        {
            var playerLevelStatsData = playerDataModel.playerData.GetOrCreatePlayerLevelStatsData(beatmapKey);
            highScore = playerLevelStatsData.validScore ? playerLevelStatsData.highScore : 0;
        }
    }

    class ResultsViewControllerPatches : IAffinity
    {
        [AffinityPatch(typeof(ResultsViewController), "SetDataToUI")]
        [AffinityPostfix]
        private void Postfix(TextMeshProUGUI ____scoreText, GameObject ____newHighScoreText, TextMeshProUGUI ____rankText, LevelCompletionResults ____levelCompletionResults, IReadonlyBeatmapData ____transformedBeatmapData)
        {
            int maxScore;
            double resultPercentage;
            int resultScore;
            int modifiedScore;
            int highScore = ResultsViewData.highScore;
            string rankTextLine1;
            string rankTextLine2 = "";
            string colorPositive = "#00B300";
            string colorNegative = "#FF0000";
            string positiveIndicator = "";
            LevelCompletionResults levelCompletionResults = ____levelCompletionResults;

            //Only calculate percentage, if map was successfully cleared
            if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
            {
                modifiedScore = levelCompletionResults.modifiedScore;
                maxScore = ScoreModel.ComputeMaxMultipliedScoreForBeatmap(____transformedBeatmapData);

                //use modifiedScore with negative multipliers
                if (levelCompletionResults.gameplayModifiers.noFailOn0Energy
                    || (levelCompletionResults.gameplayModifiers.enabledObstacleType != GameplayModifiers.EnabledObstacleType.All)
                    || levelCompletionResults.gameplayModifiers.noArrows
                    || levelCompletionResults.gameplayModifiers.noBombs
                    || levelCompletionResults.gameplayModifiers.zenMode
                    || levelCompletionResults.gameplayModifiers.songSpeed == GameplayModifiers.SongSpeed.Slower
                    )
                {
                    resultScore = modifiedScore;
                }
                //use rawScore without and with positive modifiers to avoid going over 100% without recalculating maxScore
                else
                {
                    resultScore = levelCompletionResults.multipliedScore;
                }

                resultPercentage = ScoreCalc.CalculatePercentage(maxScore, resultScore);

                //disable wrapping and autosize (unneccessary?)
                ____rankText.autoSizeTextContainer = false;
                ____rankText.enableWordWrapping = false;

                //Rank Text Changes
                //Set Percentage to first line
                rankTextLine1 = $"<line-height=27.5%><size=60%>{resultPercentage:F2}<size=45%>%";

                // Add Percent Difference to 2nd Line if enabled and previous Score exists
                if (highScore != 0)
                {
                    double oldPercentage = ScoreCalc.CalculatePercentage(maxScore, highScore);
                    double percentageDifference = resultPercentage - oldPercentage;
                    string percentageDifferenceColor;
                    //Better or same Score
                    if (percentageDifference >= 0)
                    {
                        percentageDifferenceColor = colorPositive;
                        positiveIndicator = "+";
                    }
                    //Worse Score
                    else
                    {
                        percentageDifferenceColor = colorNegative;
                        positiveIndicator = "";
                        //Fix negative score rounding to exactly 0% just showing 0% instead of -0%
                        if (Math.Round(percentageDifference, 2) == 0)
                        {
                            positiveIndicator = "-";
                        }
                    }
                    rankTextLine2 = $"\n<color={percentageDifferenceColor}><size=40%>{positiveIndicator}{percentageDifference:F2}<size=30%>%";
                }
                ____newHighScoreText.SetActive(false);

                ____rankText.text = rankTextLine1 + rankTextLine2;

                //Add ScoreDifference Calculation if enabled
                if (highScore != 0)
                {
                    string scoreDifference;
                    string scoreDifferenceColor = "";
                    scoreDifference = ScoreFormatter.Format(modifiedScore - highScore);
                    //Better Score
                    if ((modifiedScore - highScore) >= 0)
                    {
                        scoreDifferenceColor = colorPositive;
                        positiveIndicator = "+";
                    }
                    //Worse Score
                    else if ((modifiedScore - highScore) < 0)
                    {
                        scoreDifferenceColor = colorNegative;
                        positiveIndicator = "";
                    }

                    //Build new ScoreText string
                    ____scoreText.text = "<line-height=27.5%><size=60%>" + ScoreFormatter.Format(modifiedScore) + "\n"
                            + "<size=40%><color=" + scoreDifferenceColor + "><size=40%>" + positiveIndicator + scoreDifference;

                    ____newHighScoreText.SetActive(false);
                }
            }
            ResultsViewData.highScore = 0;
        }
    }

    static class ScoreCalc
    {
        public static double CalculatePercentage(int maxScore, int resultScore)
        {
            double resultPercentage = (double)(100 / (double)maxScore * (double)resultScore);
            return resultPercentage;
        }
    }
}