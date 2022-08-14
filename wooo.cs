using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Globalization;
using Microsoft.VisualBasic.FileIO;
namespace lastpokerlibrary
{
    public static class PreFlopOperations
    {
        public static double[,] PreflopMatrixSuited = new double[14, 14];
        public static double[,] PreflopMatrixUnsuited = new double[14, 14];

        public static void GenerateValueMatrix(double[,] matrix, string path)
        {
            using (TextFieldParser parser = new TextFieldParser(path))
            {
                bool firstLine = true;
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                int i = 0;
                int j = 0;
                while (!parser.EndOfData)
                {

                    //Processing row
                    string[] fields = parser.ReadFields();
                    if (firstLine)
                    {
                        firstLine = false;

                        continue;
                    }
                newLine:
                    bool firstField = true;
                    foreach (string field in fields)
                    {
                        if (firstField)
                        {
                            firstField = false;

                            continue;
                        }
                        if (!String.IsNullOrEmpty(field))
                        {
                            string newField = field.Replace(',', '.').Trim(new char[] { '%', '"' });
                            double percentNumber = double.Parse(newField, CultureInfo.InvariantCulture);
                            double frac = percentNumber / 100;
                            matrix[i, j] = frac;
                        }

                        i++;
                        if (i > 13)
                        {
                            i = 0;
                            j++;
                            goto newLine;
                        }
                    }

                }
            }
        }
    }
    public static class BotOperations
    {

    }
    public static class HandEvaluation
    {
        public static ulong Deck = 0b_11111111_1111100_11111111_1111100_11111111_1111100_11111111_1111100;
        public static Dictionary<ulong, (int, long)> _scoreTable = new Dictionary<ulong, (int, long)>();
        public static Dictionary<(ulong, ulong), (int, long)> _handTable = new Dictionary<(ulong, ulong), (int, long)>();
        public static Dictionary<ulong, long> _tieBreakerTable = new Dictionary<ulong, long>();
        public static Dictionary<(ulong, bool), int> _mainScoreTable = new Dictionary<(ulong, bool), int>();
        public static ulong ParseAsBitField(List<KeyValuePair<int, int>> cards)
        {
            ulong bf = 0;
            foreach (var card in cards)
            {
                bf |= 1UL << (card.Key + (15 * card.Value));
            }
            return bf;

        }
        private static bool isStraight(int solo)
        {
            int lsb = solo & -solo;

            int normalized = solo / lsb;

            return normalized == 31;

        }
        public static int getMainScore(int solo, ulong ranksField, bool flush)
        {
            if (_mainScoreTable.ContainsKey((ranksField, flush)))
            {
                return _mainScoreTable[(ranksField, flush)];
            }
            bool straight = isStraight(solo);
            if (straight && flush)
            {
                if (solo == 31744)
                {

                    return 10;
                }
                else
                {
                    return 9;
                }

            }
            else
            {
                switch (ranksField % 15)
                {
                    case 1:
                        return 8;
                    case 10:
                        return 7;

                    case 9:
                        return 4;
                    case 7:
                        return 3;
                    case 6:
                        return 2;
                    default:
                        break;
                }

                if (flush)
                {
                    return 6;
                }
                else if (straight || solo == 16444)
                {
                    return 5;
                }
                return 1;

            }
        }
        private static int getHighestRank(ulong ranksField, ref int pos)
        {
            pos = 63 - BitOperations.LeadingZeroCount((ulong)ranksField | 1);

            //Console.WriteLine((int)Math.Floor((double)(pos / 4)));
            return (int)Math.Floor((double)(pos / 4));
        }
        private static long getTieBreaker(ulong ranksField)
        {
            if (_tieBreakerTable.ContainsKey(ranksField))
            {
                return _tieBreakerTable[ranksField];
            }

            int pos = 0;
            int tiebreaker = 0;
            for (int i = 0; i < 5; i++)
            {
                int highestRank = getHighestRank(ranksField, ref pos);
                ranksField ^= (1UL << pos);
                tiebreaker |= (highestRank << (16 - (4 * i)));
            }


            _tieBreakerTable[ranksField] = tiebreaker;
            return tiebreaker;
        }
        private static void getFields(ulong bf, out int solo, out ulong ranksField, out bool flush)
        {
            solo = 0;
            ranksField = 0;
            flush = false;
            Dictionary<int, int> instances = new Dictionary<int, int>();
            int cards = 0;
            for (int i = 0; i < 4; i++)
            {
                int flushIdx = 0;
                for (int j = 2; j <= 14; j++)
                {


                    if ((bf & (1UL << (j + (15 * i)))) > 0)
                    {
                        cards++;
                        solo |= (1 << j);
                        flushIdx++;
                        if (flushIdx == 5)
                        {
                            flush = true;
                        }
                        if (!instances.ContainsKey(j))
                        {
                            instances.Add(j, 0);
                        }
                        else
                        {
                            instances[j] = instances[j] + 1;
                        }

                        int offset = instances[j];
                        ulong addition = 1UL << (j << 2);
                        addition = addition << offset;
                        ranksField |= addition;

                    }

                }
            }

        }
        public static IEnumerable<ulong> ToIEnum(this ulong num)
        {
            for (int i = 2; i <= 60; i++)
            {
                if ((num & (1UL << i)) > 0)
                {
                    yield return 1UL << i;
                }
            }
        }
        private static IEnumerable<ulong> cardCombos(IEnumerable<ulong> cards, int count)
        {
            int i = 0;
            foreach (var card in cards)
            {
                if (count == 1)
                {
                    yield return card;
                }

                else
                {
                    foreach (var result in cardCombos(cards.Skip(i + 1), count - 1))
                    {

                        yield return result | card;
                    }
                }

                ++i;
            }
        }

        public static (int mainScore, long tieBreaker) GetFullScore(ulong bf)
        {
            if (_scoreTable.ContainsKey(bf))
            {
                return _scoreTable[bf];
            }
            getFields(bf, out int solo, out ulong ranksField, out bool flush);
            int mainScore = getMainScore(solo, ranksField, flush);
            (int, long) result = (mainScore, getTieBreaker(ranksField));
            _mainScoreTable[(ranksField, flush)] = mainScore;
            _scoreTable[bf] = result;
            return result;
        }
        public static (int mainScore, long tieBreaker) HandToPlay(ulong holes, ulong cardsOnTable)
        {
            if (_handTable.ContainsKey((holes, cardsOnTable)))
            {
                return _handTable[(holes, cardsOnTable)];
            }

            (int mainScore, long tieBreaker) max = (-100000, -100000);
            foreach (ulong combo in cardCombos(cardsOnTable.ToIEnum(), 3))
            {
                (int mainScore, long tieBreaker) currentScore = GetFullScore(combo | holes);
                if ((currentScore.mainScore.CompareTo(max.mainScore) != 0 ? currentScore.mainScore.CompareTo(max.mainScore) : currentScore.tieBreaker.CompareTo(max.tieBreaker)) > 0)
                {
                    max = currentScore;
                }

            }
            foreach (ulong combo in cardCombos(cardsOnTable.ToIEnum(), 4))
            {
                foreach (ulong holeCard in holes.ToIEnum())
                {
                    (int mainScore, long tieBreaker) currentScore = GetFullScore(combo | holeCard);
                    if ((currentScore.mainScore.CompareTo(max.mainScore) != 0 ? currentScore.mainScore.CompareTo(max.mainScore) : currentScore.tieBreaker.CompareTo(max.tieBreaker)) > 0)
                    {
                        max = currentScore;
                    }

                }
            }
            _handTable[(holes, cardsOnTable)] = max;
            return max;
        }
    }
}
