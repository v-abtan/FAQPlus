﻿// <copyright file="WelcomeMessageViewModel.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
namespace Microsoft.Teams.Apps.FAQPlusPlus.Configuration.Models
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Represents WelcomeMessageViewModel object that hold welcome message text.
    /// </summary>
    public class WelcomeMessageViewModel
    {
        /// <summary>
        /// Gets or sets welcome message text box to be used in View.
        /// </summary>
        [Required(ErrorMessageResourceName = "WelcomeTextRequiredMessage", ErrorMessageResourceType = typeof(Strings))]
        [StringLength(maximumLength: 300, ErrorMessageResourceName = "WelcomeTextValidationMessage", ErrorMessageResourceType = typeof(Strings), MinimumLength = 2)]
        [DataType(DataType.Text)]
        [Display(Name = "Welcome message")]
        public string WelcomeMessage { get; set; }
    }
}