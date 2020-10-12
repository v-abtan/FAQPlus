// <copyright file="TeamViewModel.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Configuration.Models
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Represents TeamViewModel object to store team details.
    /// </summary>
    public class TeamViewModel
    {
        /// <summary>
        /// Gets or sets team id textbox to be used in View.
        /// </summary>
        [Required(ErrorMessageResourceName = "TeamIdRequiredMessage", ErrorMessageResourceType = typeof(Strings))]
        [MinLength(1)]
        [DataType(DataType.Text)]
        [Display(Name = "Team ID")]
        [RegularExpression(@"(\S)+", ErrorMessageResourceName = "TeamIdValidationMessage", ErrorMessageResourceType = typeof(Strings))]
        public string TeamId { get; set; }
    }
}