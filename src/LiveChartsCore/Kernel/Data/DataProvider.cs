﻿// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel.Sketches;
using System;
using System.Collections.Generic;

namespace LiveChartsCore.Kernel.Data
{
    /// <summary>
    /// Defines a data provider.
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <typeparam name="TDrawingContext"></typeparam>
    public class DataProvider<TModel, TDrawingContext>
        where TDrawingContext : DrawingContext
    {
        private readonly Dictionary<object, Dictionary<int, ChartPoint>> _byChartbyValueVisualMap = new();
        private readonly Dictionary<object, Dictionary<TModel, ChartPoint>> _byChartByReferenceVisualMap = new();
        private readonly bool _isValueType = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataProvider{TModel, TDrawingContext}"/> class.
        /// </summary>
        public DataProvider()
        {
            var t = typeof(TModel);
            _isValueType = t.IsValueType;
        }

        /// <summary>
        /// Fetches the the points for the specified series.
        /// </summary>
        /// <param name="series">The series.</param>
        /// <param name="chart">The chart.</param>
        /// <returns></returns>
        public virtual IEnumerable<ChartPoint> Fetch(ISeries<TModel> series, IChart chart)
        {
            if (series.Values == null) yield break;

            var mapper = series.Mapping ?? LiveCharts.CurrentSettings.GetMap<TModel>();
            var index = 0;

            if (_isValueType)
            {
                var canvas = (MotionCanvas<TDrawingContext>)chart.Canvas;
                _ = _byChartbyValueVisualMap.TryGetValue(canvas.Sync, out var d);
                if (d == null)
                {
                    d = new Dictionary<int, ChartPoint>();
                    _byChartbyValueVisualMap[canvas.Sync] = d;
                }
                var byValueVisualMap = d;

                foreach (var item in series.Values)
                {
                    if (!byValueVisualMap.TryGetValue(index, out var cp))
                        byValueVisualMap[index] = cp = new ChartPoint(chart.View, series);

                    cp.Context.Index = index++;
                    cp.Context.DataSource = item;

                    mapper(item, cp);

                    yield return cp;
                }
            }
            else
                foreach (var item in series.Values)
                {
                    var canvas = (MotionCanvas<TDrawingContext>)chart.Canvas;
                    _ = _byChartByReferenceVisualMap.TryGetValue(canvas.Sync, out var d);
                    if (d == null)
                    {
                        d = new Dictionary<TModel, ChartPoint>();
                        _byChartByReferenceVisualMap[canvas.Sync] = d;
                    }
                    var byReferenceVisualMap = d;

                    if (!byReferenceVisualMap.TryGetValue(item, out var cp))
                        byReferenceVisualMap[item] = cp = new ChartPoint(chart.View, series);

                    cp.Context.Index = index++;
                    cp.Context.DataSource = item;
                    mapper(item, cp);

                    yield return cp;
                }
        }

        /// <summary>
        /// Disposes a given point.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns></returns>
        public void DisposePoint(ChartPoint point)
        {
            if (_isValueType)
            {
                var canvas = (MotionCanvas<TDrawingContext>)point.Context.Chart.CoreChart.Canvas;
                _ = _byChartbyValueVisualMap.TryGetValue(canvas.Sync, out var d);
                var byValueVisualMap = d;
                if (d == null) return;
                _ = byValueVisualMap.Remove(point.Context.Index);
            }
            else
            {
                if (point.Context.DataSource == null) return;
                var canvas = (MotionCanvas<TDrawingContext>)point.Context.Chart.CoreChart.Canvas;
                _ = _byChartByReferenceVisualMap.TryGetValue(canvas.Sync, out var d);
                var byReferenceVisualMap = d;
                if (d == null) return;
                _ = byReferenceVisualMap.Remove((TModel)point.Context.DataSource);
            }
        }

        /// <summary>
        /// Gets the Cartesian bounds.
        /// </summary>
        /// <param name="chart">The chart.</param>
        /// <param name="series">The series.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns></returns>
        public virtual DimensionalBounds GetCartesianBounds(
            CartesianChart<TDrawingContext> chart,
            IChartSeries<TDrawingContext> series,
            IAxis<TDrawingContext> x,
            IAxis<TDrawingContext> y)
        {
            var stack = chart.SeriesContext.GetStackPosition(series, series.GetStackGroup());

            var xMin = x.MinLimit ?? float.MinValue;
            var xMax = x.MaxLimit ?? float.MaxValue;
            var yMin = y.MinLimit ?? float.MinValue;
            var yMax = y.MaxLimit ?? float.MaxValue;

            var hasData = false;

            var bounds = new DimensionalBounds();
            ChartPoint? previous = null;
            foreach (var point in series.Fetch(chart))
            {
                var primary = point.PrimaryValue;
                var secondary = point.SecondaryValue;
                var tertiary = point.TertiaryValue;

                if (stack != null) primary = stack.StackPoint(point);

                bounds.PrimaryBounds.AppendValue(primary);
                bounds.SecondaryBounds.AppendValue(secondary);
                bounds.TertiaryBounds.AppendValue(tertiary);

                if (primary >= yMin && primary <= yMax && secondary >= xMin && secondary <= xMax)
                {
                    bounds.VisiblePrimaryBounds.AppendValue(primary);
                    bounds.VisibleSecondaryBounds.AppendValue(secondary);
                    bounds.VisibleTertiaryBounds.AppendValue(tertiary);
                }

                if (previous != null)
                {
                    var dx = Math.Abs(previous.SecondaryValue - point.SecondaryValue);
                    var dy = Math.Abs(previous.PrimaryValue - point.PrimaryValue);
                    if (dx < bounds.MinDeltaSecondary) bounds.MinDeltaSecondary = dx;
                    if (dy < bounds.MinDeltaPrimary) bounds.MinDeltaPrimary = dy;
                }

                previous = point;
                hasData = true;
            }

            if (!hasData)
                if (x.PreviousDataBounds == null || y.PreviousDataBounds == null)
                {
                    bounds.VisiblePrimaryBounds.AppendValue(0);
                    bounds.VisiblePrimaryBounds.AppendValue(10);
                    bounds.PrimaryBounds.AppendValue(0);
                    bounds.PrimaryBounds.AppendValue(10);

                    bounds.VisibleSecondaryBounds.AppendValue(0);
                    bounds.VisibleSecondaryBounds.AppendValue(10);
                    bounds.SecondaryBounds.AppendValue(0);
                    bounds.SecondaryBounds.AppendValue(10);

                    bounds.VisibleTertiaryBounds.AppendValue(1);
                    bounds.TertiaryBounds.AppendValue(1);
                }
                else
                {
                    bounds.VisiblePrimaryBounds.AppendValue(x.PreviousDataBounds.Max);
                    bounds.VisiblePrimaryBounds.AppendValue(x.PreviousDataBounds.Min);
                    bounds.PrimaryBounds.AppendValue(x.PreviousDataBounds.Max);
                    bounds.PrimaryBounds.AppendValue(x.PreviousDataBounds.Min);

                    bounds.VisibleSecondaryBounds.AppendValue(y.PreviousDataBounds.Max);
                    bounds.VisibleSecondaryBounds.AppendValue(y.PreviousDataBounds.Min);
                    bounds.SecondaryBounds.AppendValue(y.PreviousDataBounds.Max);
                    bounds.SecondaryBounds.AppendValue(y.PreviousDataBounds.Min);

                    bounds.VisibleTertiaryBounds.AppendValue(1);
                    bounds.TertiaryBounds.AppendValue(1);
                }

            return bounds;
        }

        /// <summary>
        /// Gets the pie bounds.
        /// </summary>
        /// <param name="chart">The chart.</param>
        /// <param name="series">The series.</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">Unexpected null stacker</exception>
        public virtual DimensionalBounds GetPieBounds(
            PieChart<TDrawingContext> chart, IPieSeries<TDrawingContext> series)
        {
            var stack = chart.SeriesContext.GetStackPosition(series, series.GetStackGroup());
            if (stack == null) throw new NullReferenceException("Unexpected null stacker");

            var bounds = new DimensionalBounds();
            var hasData = false;

            foreach (var point in series.Fetch(chart))
            {
                _ = stack.StackPoint(point);
                bounds.PrimaryBounds.AppendValue(point.PrimaryValue);
                bounds.SecondaryBounds.AppendValue(point.SecondaryValue);
                bounds.TertiaryBounds.AppendValue(series.Pushout > series.HoverPushout ? series.Pushout : series.HoverPushout);
                hasData = true;
            }

            if (!hasData)
            {
                bounds.PrimaryBounds.AppendValue(0);
                bounds.SecondaryBounds.AppendValue(0);
                bounds.TertiaryBounds.AppendValue(0);
            }

            return bounds;
        }

        /// <summary>
        /// Clears the visuals in the cache.
        /// </summary>
        /// <returns></returns>
        public virtual void RestartVisuals()
        {
            foreach (var byReferenceVisualMap in _byChartByReferenceVisualMap)
            {
                foreach (var item in byReferenceVisualMap.Value)
                {
                    if (item.Value.Context.Visual is not IAnimatable visual) continue;
                    visual.RemoveTransitions();
                }
                byReferenceVisualMap.Value.Clear();
            }

            foreach (var byValueVisualMap in _byChartbyValueVisualMap)
            {
                foreach (var item in byValueVisualMap.Value)
                {
                    if (item.Value.Context.Visual is not IAnimatable visual) continue;
                    visual.RemoveTransitions();
                }
                byValueVisualMap.Value.Clear();
            }
        }
    }
}
