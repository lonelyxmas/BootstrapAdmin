﻿using System;
using System.Collections.Generic;

namespace Bootstrap.DataAccess
{
    /// <summary>
    /// 
    /// </summary>
    public class Log
    {
        /// <summary>
        /// 获得/设置 操作日志主键ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 获得/设置 操作类型
        /// </summary>
        public string CRUD { get; set; }

        /// <summary>
        /// 获得/设置 用户名称
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 获得/设置 操作时间
        /// </summary>
        public DateTime LogTime { get; set; }

        /// <summary>
        /// 获得/设置 客户端IP
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string City { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Browser { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string OS { get; set; }

        /// <summary>
        /// 获取/设置 请求网址
        /// </summary>
        public string RequestUrl { get; set; }

        /// <summary>
        /// 获得/设置 请求数据
        /// </summary>
        public string RequestData { get; set; }

        /// <summary>
        /// 查询所有日志信息
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<Log> Retrieves() => DbManager.Create().Fetch<Log>("select * from Logs where LogTime > @0 order by LogTime desc", DateTime.Now.AddDays(-7));

        /// <summary>
        /// 删除日志信息
        /// </summary>
        /// <returns></returns>
        private static void DeleteLogAsync()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                var dtm = DateTime.Now.AddMonths(0 - DictHelper.RetrieveLogsPeriod());
                DbManager.Create().Execute("delete from Logs where LogTime < @0", dtm);
            });
        }

        /// <summary>
        /// 保存新增的日志信息
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual bool Save(Log p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            DeleteLogAsync();
            p.LogTime = DateTime.Now;
            DbManager.Create().Save(p);
            return true;
        }
    }
}
