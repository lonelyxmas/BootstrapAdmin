﻿using Bootstrap.DataAccess;
using Bootstrap.Security;
using Longbow.Cache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Generic;

namespace Bootstrap.Admin.Controllers.Api
{
    /// <summary>
    /// 
    /// </summary>
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [HttpPost]
        [ButtonAuthorize(Url = "~/Admin/Settings", Auth = "saveTitle,saveFooter,saveTheme,saveUISettings")]
        public bool Post([FromBody]BootstrapDict value) => DictHelper.SaveSettings(value);

        /// <summary>
        /// 
        /// </summary>
        [HttpGet]
        public IEnumerable<ICacheCorsItem> Get() => CacheManager.CorsSites;
    }
}
