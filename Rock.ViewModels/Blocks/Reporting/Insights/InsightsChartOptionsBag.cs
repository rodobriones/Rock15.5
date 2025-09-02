using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rock.ViewModels.Blocks.Reporting.Insights
{
    /// <summary>
    /// Data Bag for the Insights block charts.
    /// </summary>
    public class InsightsChartOptionsBag
    {

        /// <summary>
        /// Gets or sets the chart title.
        /// </summary>
        public string ChartTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the chart (e.g., bar, pie).
        /// </summary> 
        public string ChartType { get; set; } = string.Empty;


        /// <summary>
        /// Gets or sets a value indicating whether the chart legend is enabled.
        /// </summary>
        public bool IsChartLegendEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the X axis title.
        /// </summary>
        public string XTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Y axis title.
        /// </summary>
        public string YTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the X axis legend is enabled.
        /// </summary>
        public bool IsXLegendEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Y axis legend is enabled.
        /// </summary>
        public bool IsYLegendEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum value for the Y axis.
        /// </summary>
        public int YMax { get; set; } = 100;

        /// <summary>
        /// Gets or sets the step size for the Y axis.
        /// </summary>
        public int YStepSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets a value indicating whether the chart should maintain aspect ratio.
        /// </summary>
        public bool IsMaintainAspectRatio { get; set; } = true;

        /// <summary>
        /// Gets or sets the aspect ratio of the chart.
        /// </summary>
        public int AspectRatio { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether the chart is responsive.
        /// </summary>
        public bool IsResponsive { get; set; } = true;
        
    }
}
