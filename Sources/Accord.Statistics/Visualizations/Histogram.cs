// Accord Statistics Library
// The Accord.NET Framework
// http://accord-framework.net
//
// Copyright © César Souza, 2009-2013
// cesarsouza at gmail.com
//
//    This library is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 2.1 of the License, or (at your option) any later version.
//
//    This library is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public
//    License along with this library; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
//

namespace Accord.Statistics.Visualizations
{
    using System;
    using Accord.Math;
    using AForge;
    using System.Diagnostics;

    /// <summary>
    ///   Optimum histogram bin size adjustment rule.
    /// </summary>
    /// 
    public enum BinAdjustmentRule
    {
        /// <summary>
        ///   Does not attempts to automatically calculate 
        ///   an optimum bin width and preserves the current
        ///   histogram organization.
        /// </summary>
        /// 
        None,

        /// <summary>
        ///   Calculates the optimum bin width as 3.49σN, where σ 
        ///   is the sample standard deviation and N is the number
        ///   of samples.
        /// </summary>
        /// <remarks>
        ///   Scott, D. 1979. On optimal and data-based histograms. Biometrika, 66:605-610.
        /// </remarks>
        /// 
        Scott,

        /// <summary>
        ///   Calculates the optimum bin width as ceiling( log2(N) + 1 )m
        ///   where N is the number of samples. The rule implicitly bases
        ///   the bin sizes on the range of the data, and can perform poorly
        ///   if n &lt; 30.
        /// </summary>
        /// 
        Sturges,

        /// <summary>
        ///   Calculates the optimum bin width as the square root of the
        ///   number of samples. This is the same rule used by Microsoft (c)
        ///   Excel and many others.
        /// </summary>
        /// 
        SquareRoot,
    }

    /// <summary>
    ///   Histogram.
    /// </summary>
    /// 
    /// <remarks>
    ///  <para>
    ///   In a more general mathematical sense, a histogram is a mapping Mi
    ///   that counts the number of observations that fall into various 
    ///   disjoint categories (known as bins).</para>
    ///  <para>
    ///   This class represents a Histogram mapping of Discrete or Continuous
    ///   data. To use it as a discrete mapping, pass a bin size (length) of 1.
    ///   To use it as a continuous mapping, pass any real number instead.</para>
    ///  <para>
    ///   Currently, only a constant bin width is supported.</para>
    /// </remarks>
    /// 
    [Serializable]
    public class Histogram
    {

        private int[] binValues;
        private double[] binRanges;

        private bool inclusiveUpperBound = true;

        private HistogramBinCollection binCollection;

        private bool cumulative;
        private string title = "Histogram";

        private BinAdjustmentRule rule = BinAdjustmentRule.SquareRoot;


        //---------------------------------------------


        #region Constructors
        /// <summary>
        ///   Constructs an empty histogram
        /// </summary>
        public Histogram()
            : this(String.Empty)
        {
        }

        /// <summary>
        ///   Constructs an empty histogram
        /// </summary>
        /// <param name="title">The title of this histogram.</param>
        public Histogram(String title)
        {
            this.title = title;
            initialize(0);
        }
        #endregion


        //---------------------------------------------


        #region Properties
        /// <summary>Gets the Bin values of this Histogram.</summary>
        /// <param name="index">Bin index.</param>
        /// <returns>The number of hits of the selected bin.</returns>
        public int this[int index]
        {
            get { return binValues[index]; }
        }

        /// <summary>Gets the name for this Histogram.</summary>
        public String Title
        {
            get { return this.title; }
            set { this.title = value; }
        }

        /// <summary>Gets the Bin values for this Histogram.</summary>
        public int[] Values
        {
            get { return binValues; }
        }

        /// <summary>Gets the Range of the values in this Histogram.</summary>
        public DoubleRange Range
        {
            get { return new DoubleRange(binRanges[0], binRanges[binRanges.Length - 1]); }
        }

        /// <summary>Gets the edges of each bin in this Histogram.</summary>
        public double[] Edges
        {
            get { return binRanges; }
        }

        /// <summary>Gets the collection of bins of this Histogram.</summary>
        public HistogramBinCollection Bins
        {
            get { return binCollection; }
        }

        /// <summary>
        ///   Gets or sets whether this histogram represents a cumulative distribution.
        /// </summary>
        public bool Cumulative
        {
            get { return this.cumulative; }
            set { this.cumulative = value; }
        }


        /// <summary>
        ///   Gets or sets the bin size auto adjustment rule
        ///   to be used when computing this histogram from
        ///   new data. Default is <see cref="BinAdjustmentRule.SquareRoot"/>.
        /// </summary>
        /// <value>The bin size auto adjustment rule.</value>
        public BinAdjustmentRule AutoAdjustmentRule
        {
            get { return rule; }
            set { rule = value; }
        }


        /// <summary>
        ///   Gets or sets a value indicating whether the last bin
        ///   should have an inclusive upper bound. Default is <c>true</c>.
        /// </summary>
        /// <remarks>
        ///   If set to <c>false</c>, the last bin's range will be defined
        ///   as Edge[i] &lt;= x &lt; Edge[i+1]. If set to <c>true</c>, the
        ///   last bin will have an inclusive upper bound and be defined as
        ///   Edge[i] &lt;= x &lt;= Edge[i+1] instead.
        /// </remarks>
        /// <value>
        ///   <c>true</c> if the last bin should have an inclusive upper bound;
        ///   <c>false</c> otherwise.
        /// </value>
        public bool InclusiveUpperBound
        {
            get { return inclusiveUpperBound; }
            set { inclusiveUpperBound = value; }
        }
        #endregion


        //---------------------------------------------


        #region Public Methods

        /// <summary>
        ///   Computes (populates) an Histogram mapping with values from a sample. 
        /// </summary>
        public void Compute(double[] values, double binWidth)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (binWidth <= 0.0)
                throw new ArgumentOutOfRangeException("binWidth");

            // Compute values' range
            DoubleRange range = values.Range();

            // Determine number of bins based on the given width
            int numberOfBins = (int)Math.Ceiling(range.Length / binWidth);

            // Create bin structure
            initialize(numberOfBins);

            // Create ranges w/ fixed width
            initialize(range.Min, binWidth);

            // Create histogram
            this.compute(values);
        }

        /// <summary>
        ///   Computes (populates) an Histogram mapping with values from a sample. 
        /// </summary>
        public void Compute(double[] values, int numberOfBins)
        {
            Compute(values, numberOfBins, false);
        }

        /// <summary>
        ///   Computes (populates) an Histogram mapping with values from a sample. 
        /// </summary>
        public void Compute(double[] values, int numberOfBins, bool extraUpperBin)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (numberOfBins <= 0)
                throw new ArgumentOutOfRangeException("numberOfBins");

            // Compute values' range
            DoubleRange range = values.Range();

            // Determine bin width based on the given number of bins
            double binWidth = range.Length / (double)numberOfBins;

            if (extraUpperBin) numberOfBins++;

            // Create bin structure
            initialize(numberOfBins);

            // Create ranges w/ fixed width
            initialize(range.Min, binWidth);

            // Create histogram
            this.compute(values);
        }

        /// <summary>
        ///   Computes (populates) an Histogram mapping with values from a sample. 
        /// </summary>
        public void Compute(double[] values, int numberOfBins, double binWidth)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (numberOfBins <= 0)
                throw new ArgumentOutOfRangeException("numberOfBins");

            if (binWidth <= 0.0)
                throw new ArgumentOutOfRangeException("binWidth");

            // Compute values' range
            DoubleRange range = values.Range();

            // Create bin structure
            initialize(numberOfBins);

            // Create ranges w/ fixed width
            initialize(range.Min, binWidth);

            // Create histogram
            this.compute(values);
        }

        /// <summary>
        ///   Computes (populates) an Histogram mapping with values from a sample. 
        /// </summary>
        public void Compute(double[] values)
        {
            // Compute values' range
            DoubleRange range = values.Range();

            // Check if there are no values
            if (values.Length == 0)
            {
                initialize(0);
            }

            // Check if we have a constant value
            else if (range.Length == 0)
            {
                // Yes, we will create a special histogram
                // bin to accommodate those constant values.
                initialize(1);

                binRanges[0] = range.Min;
                binRanges[1] = range.Max;
            }

            // Check if we have to auto-adjust the histogram
            //  bins according to some selection choice.
            else if (rule != BinAdjustmentRule.None)
            {
                // Yes, we will be recomputing the optimal number of bins
                int numberOfBins = computeNumberOfBins(values, range, rule);

                // Determine bin width based on the given number of bins
                double binWidth = range.Length / (double)numberOfBins;

                // Create bin structure
                initialize(numberOfBins);

                // Create ranges w/ fixed width
                initialize(range.Min, binWidth);
            }

            // Create histogram
            this.compute(values);
        }

        #endregion


        //---------------------------------------------


        #region Private Methods

        /// <summary>
        ///   Initializes the histogram's bins.
        /// </summary>
        private void initialize(int numberOfBins)
        {
            this.binValues = new int[numberOfBins];
            this.binRanges = new double[numberOfBins + 1];

            HistogramBin[] bins = new HistogramBin[numberOfBins];
            for (int i = 0; i < numberOfBins; i++)
                bins[i] = new HistogramBin(this, i);
            binCollection = new HistogramBinCollection(bins);
        }

        /// <summary>
        ///   Sets the histogram's bin ranges (edges).
        /// </summary>
        private void initialize(double startValue, double width)
        {
            binRanges[0] = startValue;
            for (int i = 1; i < binRanges.Length; i++)
                binRanges[i] = binRanges[i - 1] + width;
        }

        /// <summary>
        ///   Actually computes the histogram.
        /// </summary>
        private void compute(double[] values)
        {
            int numberOfBins = binValues.Length;
            DoubleRange scale = new DoubleRange(0, numberOfBins);
            DoubleRange range = Range;

            // Populate Bins
            for (int i = 0; i < values.Length; i++)
            {
                double v = values[i];

                // Convert the value to the range of histogram
                //  bins to check which bin the value belongs.
                double binIndex = Tools.Scale(range, scale, v);
                int index = (int)binIndex;

                if (index >= 0 && index < numberOfBins)
                    binValues[index]++;

                if (inclusiveUpperBound && index == numberOfBins)
                    binValues[numberOfBins - 1]++;
            }


            // If this is a cumulative histogram,
            //  accumulate values in the bins.
            if (cumulative)
            {
                for (int i = 1; i < this.binValues.Length; i++)
                    binValues[i] += binValues[i - 1];
            }
        }

        /// <summary>
        ///   Computes the optimum number of bins based on a <see cref="BinAdjustmentRule"/>.
        /// </summary>
        private static int computeNumberOfBins(double[] values, DoubleRange range, BinAdjustmentRule rule)
        {
            switch (rule)
            {
                case BinAdjustmentRule.None:
                    return 0;

                case BinAdjustmentRule.Scott:
                    double h = (3.49 * Statistics.Tools.StandardDeviation(values))
                        / System.Math.Pow(values.Length, 1.0 / 3.0);
                    return (int)Math.Ceiling(range.Length / h);

                case BinAdjustmentRule.Sturges:
                    return (int)Math.Ceiling(Math.Log(values.Length, 2));

                case BinAdjustmentRule.SquareRoot:
                    return (int)Math.Floor(Math.Sqrt(values.Length));

                default:
                    goto case BinAdjustmentRule.SquareRoot;
            }
        }

        #endregion


        //---------------------------------------------


        #region Operators
        /// <summary>
        ///   Integer array implicit conversion.
        /// </summary>
        public static implicit operator int[](Histogram value)
        {
            return value.binValues;
        }

        /// <summary>
        ///   Converts this histogram into an integer array representation.
        /// </summary>
        public int[] ToArray()
        {
            return this.binValues;
        }
        #endregion




    }





    /// <summary>
    ///   Collection of Histogram bins. This class cannot be instantiated.
    /// </summary>
    /// 
    [Serializable]
    public class HistogramBinCollection : System.Collections.ObjectModel.ReadOnlyCollection<HistogramBin>
    {
        internal HistogramBinCollection(HistogramBin[] objects)
            : base(objects)
        {

        }

        /// <summary>
        ///   Searches for a bin containing the specified value.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <returns>The histogram bin containing the searched value.</returns>
        public HistogramBin Search(double value)
        {
            foreach (HistogramBin bin in this)
            {
                if (bin.Contains(value))
                    return bin;
            }

            return null;
        }

        /// <summary>
        ///   Attempts to find the index of the bin containing the specified value.
        /// </summary>
        /// 
        /// <param name="value">The value to search for.</param>
        /// 
        /// <returns>The index of the bin containing the specified value.</returns>
        /// 
        public int SearchIndex(double value)
        {
            for (int i = 0; i < this.Count; i++)
            {
                if (this[i].Contains(value))
                    return i;
            }
            return -1;
        }


    }

    /// <summary>
    ///   Histogram Bin
    /// </summary>
    /// <remarks>
    /// <para>
    ///   A "bin" is a container, where each element stores the total number of observations of a sample
    ///   whose values lie within a given range. A histogram of a sample consists of a list of such bins
    ///   whose range does not overlap with each other; or in other words, bins that are mutually exclusive.</para>
    /// <para>
    ///   Unless <see cref="Histogram.InclusiveUpperBound"/> is true, the ranges of all bins <c>i</c> are
    ///   defined as Edge[i] &lt;= x &lt; Edge[i+1]. Otherwise, the last bin will have an inclusive upper
    ///   bound (i.e. will be defined as Edge[i] &lt;= x &lt;= Edge[i+1].</para>  
    /// </remarks>
    [Serializable]
    public class HistogramBin
    {

        private int index;
        private Histogram histogram;


        internal HistogramBin(Histogram histogram, int index)
        {
            this.index = index;
            this.histogram = histogram;
        }

        /// <summary>Gets the actual range of data this bin represents.</summary>
        public DoubleRange Range
        {
            get
            {
                double min = histogram.Edges[index];
                double max = histogram.Edges[index + 1];
                return new DoubleRange(min, max);
            }
        }

        /// <summary>Gets the Width (range) for this histogram bin.</summary>
        public double Width
        {
            get { return histogram.Edges[index + 1] - histogram.Edges[index]; }
        }

        /// <summary>
        ///   Gets the Value (number of occurrences of a variable in a range)
        ///   for this histogram bin.
        /// </summary>
        public int Value
        {
            get { return histogram.Values[index]; }
        }

        /// <summary>
        ///   Gets whether the Histogram Bin contains the given value.
        /// </summary>
        public bool Contains(double value)
        {
            if (histogram.InclusiveUpperBound &&
                index == histogram.Bins.Count - 1)
            {
                return value >= histogram.Edges[index] &&
                       value <= histogram.Edges[index + 1];
            }
            else
            {
                return value >= histogram.Edges[index] &&
                       value < histogram.Edges[index + 1];
            }
        }
    }
}
