﻿using Longbow;
using Longbow.Caching;
using Longbow.Caching.Configuration;
using Longbow.Data;
using Longbow.ExceptionManagement;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace Bootstrap.DataAccess
{
    /// <summary>
    /// 
    /// </summary>
    public class RoleHelper
    {
        private const string RoleDataKey = "RoleData-CodeRoleHelper";
        private const string RolebyGroupDataKey = "RoleData-CodeRoleHelper-";
        private const string RoleUserIDDataKey = "RoleData-CodeRoleHelper-";
        private const string RoleNavigationIDDataKey = "RoleData-CodeRoleHelper-Navigation";
        /// <summary>
        /// 查询所有角色
        /// </summary>
        /// <param name="tId"></param>
        /// <returns></returns>
        public static IEnumerable<Role> RetrieveRoles(string tId = null)
        {
            string sql = "select * from Roles";
            var ret = CacheManager.GetOrAdd(RoleDataKey, CacheSection.RetrieveIntervalByKey(RoleDataKey), key =>
            {
                List<Role> roles = new List<Role>();
                DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql);
                try
                {
                    using (DbDataReader reader = DBAccessManager.SqlDBAccess.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            roles.Add(new Role()
                            {
                                ID = (int)reader[0],
                                RoleName = LgbConvert.ReadValue(reader[1], string.Empty),
                                Description = LgbConvert.ReadValue(reader[2], string.Empty)
                            });
                        }
                    }
                }
                catch (Exception ex) { ExceptionManager.Publish(ex); }
                return roles;
            }, CacheSection.RetrieveDescByKey(RoleDataKey));
            return string.IsNullOrEmpty(tId) ? ret : ret.Where(t => tId.Equals(t.ID.ToString(), StringComparison.OrdinalIgnoreCase));
        }
        /// <summary>
        /// 保存用户角色关系
        /// </summary>
        /// <param name="id"></param>
        /// <param name="roleIds"></param>
        /// <returns></returns>
        public static bool SaveRolesByUserId(int id, string roleIds)
        {
            var ret = false;
            DataTable dt = new DataTable();
            dt.Columns.Add("UserID", typeof(int));
            dt.Columns.Add("RoleID", typeof(int));
            //判断用户是否选定角色
            if (!string.IsNullOrEmpty(roleIds)) roleIds.Split(',').ToList().ForEach(roleId => dt.Rows.Add(id, roleId));
            using (TransactionPackage transaction = DBAccessManager.SqlDBAccess.BeginTransaction())
            {
                try
                {
                    // delete user from config table
                    string sql = "delete from UserRole where UserID = @UserID;";
                    using (DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql))
                    {
                        cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@UserID", id, ParameterDirection.Input));
                        DBAccessManager.SqlDBAccess.ExecuteNonQuery(cmd, transaction);

                        // insert batch data into config table
                        using (SqlBulkCopy bulk = new SqlBulkCopy((SqlConnection)transaction.Transaction.Connection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction.Transaction))
                        {
                            bulk.BatchSize = 1000;
                            bulk.DestinationTableName = "UserRole";
                            bulk.ColumnMappings.Add("UserID", "UserID");
                            bulk.ColumnMappings.Add("RoleID", "RoleID");
                            bulk.WriteToServer(dt);
                            transaction.CommitTransaction();
                        }
                    }
                    ret = true;
                    ClearCache();
                }
                catch (Exception ex)
                {
                    ExceptionManager.Publish(ex);
                    transaction.RollbackTransaction();
                }
            }
            return ret;
        }
        /// <summary>
        /// 查询某个用户所拥有的角色
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Role> RetrieveRolesByUserId(int userId)
        {
            string k = string.Format("{0}{1}", RoleUserIDDataKey, userId);
            return CacheManager.GetOrAdd(k, CacheSection.RetrieveIntervalByKey(RoleUserIDDataKey), key =>
            {
                List<Role> Roles = new List<Role>();
                string sql = "select r.ID, r.RoleName, r.[Description], case ur.RoleID when r.ID then 'checked' else '' end [status] from Roles r left join UserRole ur on r.ID = ur.RoleID and UserID = @UserID";
                try
                {
                    DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql);
                    cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@UserID", userId, ParameterDirection.Input));
                    using (DbDataReader reader = DBAccessManager.SqlDBAccess.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            Roles.Add(new Role()
                            {
                                ID = (int)reader[0],
                                RoleName = (string)reader[1],
                                Description = (string)reader[2],
                                Checked = (string)reader[3]
                            });
                        }
                    }
                }
                catch (Exception ex) { ExceptionManager.Publish(ex); }
                return Roles;
            }, CacheSection.RetrieveDescByKey(RoleUserIDDataKey));
        }

        /// <summary>
        /// 删除角色表
        /// </summary>
        /// <param name="IDs"></param>
        public static bool DeleteRole(string IDs)
        {
            bool ret = false;
            if (string.IsNullOrEmpty(IDs) || IDs.Contains("'")) return ret;
            try
            {
                string sql = string.Format(CultureInfo.InvariantCulture, "Delete from Roles where ID in ({0})", IDs);
                using (DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql))
                {
                    DBAccessManager.SqlDBAccess.ExecuteNonQuery(cmd);
                    ClearCache();
                    ret = true;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.Publish(ex);
            }
            return ret;
        }
        /// <summary>
        /// 保存新建/更新的角色信息
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static bool SaveRole(Role p)
        {
            if (p == null) throw new ArgumentNullException("p");
            bool ret = false;
            if (!string.IsNullOrEmpty(p.RoleName) && p.RoleName.Length > 50) p.RoleName = p.RoleName.Substring(0, 50);
            if (!string.IsNullOrEmpty(p.Description) && p.Description.Length > 50) p.Description = p.Description.Substring(0, 500);
            string sql = p.ID == 0 ?
                "Insert Into Roles (RoleName, Description) Values (@RoleName, @Description)" :
                "Update Roles set RoleName = @RoleName, Description = @Description where ID = @ID";
            try
            {
                using (DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql))
                {
                    cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@ID", p.ID, ParameterDirection.Input));
                    cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@RoleName", p.RoleName, ParameterDirection.Input));
                    cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@Description", p.Description, ParameterDirection.Input));
                    DBAccessManager.SqlDBAccess.ExecuteNonQuery(cmd);
                }
                ret = true;
                ClearCache();
            }
            catch (DbException ex)
            {
                ExceptionManager.Publish(ex);
            }
            return ret;
        }

        /// <summary>
        /// 查询某个菜单所拥有的角色
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public static IEnumerable<Role> RetrieveRolesByMenuId(int menuId)
        {
            string sql = "select r.ID, r.RoleName, r.[Description], case ur.RoleID when r.ID then 'checked' else '' end [status] from Roles r left join NavigationRole ur on r.ID = ur.RoleID and NavigationID = @NavigationID";
            string k = string.Format("{0}{1}", RoleNavigationIDDataKey, menuId);
            var ret = CacheManager.GetOrAdd(k, CacheSection.RetrieveIntervalByKey(RoleNavigationIDDataKey), key =>
            {
                List<Role> Roles = new List<Role>();
                DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql);
                cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@NavigationID", menuId, ParameterDirection.Input));
                try
                {
                    using (DbDataReader reader = DBAccessManager.SqlDBAccess.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            Roles.Add(new Role()
                            {
                                ID = (int)reader[0],
                                RoleName = (string)reader[1],
                                Description = (string)reader[2],
                                Checked = (string)reader[3]
                            });
                        }
                    }
                }
                catch (Exception ex) { ExceptionManager.Publish(ex); }
                return Roles;
            }, CacheSection.RetrieveDescByKey(RoleNavigationIDDataKey));
            return ret;
        }
        public static bool SavaRolesByMenuId(int id, string roleIds)
        {
            var ret = false;
            DataTable dt = new DataTable();
            dt.Columns.Add("NavigationID", typeof(int));
            dt.Columns.Add("RoleID", typeof(int));
            //判断用户是否选定角色
            if (!string.IsNullOrEmpty(roleIds)) roleIds.Split(',').ToList().ForEach(roleId => dt.Rows.Add(id, roleId));
            using (TransactionPackage transaction = DBAccessManager.SqlDBAccess.BeginTransaction())
            {
                try
                {
                    // delete role from config table
                    string sql = "delete from NavigationRole where NavigationID=@NavigationID;";
                    using (DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql))
                    {
                        cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@NavigationID", id, ParameterDirection.Input));
                        DBAccessManager.SqlDBAccess.ExecuteNonQuery(cmd, transaction);

                        // insert batch data into config table
                        using (SqlBulkCopy bulk = new SqlBulkCopy((SqlConnection)transaction.Transaction.Connection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction.Transaction))
                        {
                            bulk.BatchSize = 1000;
                            bulk.DestinationTableName = "NavigationRole";
                            bulk.ColumnMappings.Add("NavigationID", "NavigationID");
                            bulk.ColumnMappings.Add("RoleID", "RoleID");
                            bulk.WriteToServer(dt);
                            transaction.CommitTransaction();
                        }
                    }
                    ret = true;
                    ClearCache();
                }
                catch (Exception ex)
                {
                    ExceptionManager.Publish(ex);
                    transaction.RollbackTransaction();
                }
            }
            return ret;
        }
        // 更新缓存
        private static void ClearCache(string cacheKey = null)
        {
            CacheManager.Clear(key => string.IsNullOrEmpty(cacheKey) || key == cacheKey);
        }

        /// <summary>
        /// 根据GroupId查询和该Group有关的所有Roles
        /// author:liuchun
        /// <param name="tId"></param>
        /// <returns></returns>
        public static IEnumerable<Role> RetrieveRolesByGroupId(int groupID)
        {
            string key = string.Format("{0}{1}", RolebyGroupDataKey, groupID);
            return CacheManager.GetOrAdd(key, CacheSection.RetrieveIntervalByKey(RolebyGroupDataKey), k =>
            {
                List<Role> Roles = new List<Role>();
                string sql = "select r.ID, r.RoleName, r.[Description], case ur.RoleID when r.ID then 'checked' else '' end [status] from Roles r left join RoleGroup ur on r.ID = ur.RoleID and GroupID = @GroupID";
                DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql);
                try
                {
                    cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@GroupID", groupID, ParameterDirection.Input));
                    using (DbDataReader reader = DBAccessManager.SqlDBAccess.ExecuteReader(cmd))
                    {
                        while (reader.Read())
                        {
                            Roles.Add(new Role()
                            {
                                ID = (int)reader[0],
                                RoleName = (string)reader[1],
                                Description = (string)reader[2],
                                Checked = (string)reader[3]
                            });
                        }
                    }
                }
                catch (Exception ex) { ExceptionManager.Publish(ex); }
                return Roles;
            }, CacheSection.RetrieveDescByKey(RolebyGroupDataKey));
        }

        /// <summary>
        /// 根据GroupId更新Roles信息，删除旧的Roles信息，插入新的Roles信息
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static bool SaveRolesByGroupId(int id, string roleIds)
        {
            var ret = false;
            //构造表格
            DataTable dt = new DataTable();
            dt.Columns.Add("RoleID", typeof(int));
            dt.Columns.Add("GroupID", typeof(int));
            if (!string.IsNullOrEmpty(roleIds)) roleIds.Split(',').ToList().ForEach(roleId => dt.Rows.Add(roleId, id));
            using (TransactionPackage transaction = DBAccessManager.SqlDBAccess.BeginTransaction())
            {
                try
                {
                    // delete user from config table
                    string sql = "delete from RoleGroup where GroupID=@GroupID";
                    using (DbCommand cmd = DBAccessManager.SqlDBAccess.CreateCommand(CommandType.Text, sql))
                    {
                        cmd.Parameters.Add(DBAccessManager.SqlDBAccess.CreateParameter("@GroupID", id, ParameterDirection.Input));
                        DBAccessManager.SqlDBAccess.ExecuteNonQuery(cmd, transaction);

                        // insert batch data into config table
                        using (SqlBulkCopy bulk = new SqlBulkCopy((SqlConnection)transaction.Transaction.Connection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction.Transaction))
                        {
                            bulk.BatchSize = 1000;
                            bulk.DestinationTableName = "RoleGroup";
                            bulk.ColumnMappings.Add("RoleID", "RoleID");
                            bulk.ColumnMappings.Add("GroupID", "GroupID");
                            bulk.WriteToServer(dt);
                            transaction.CommitTransaction();
                        }
                    }
                    ret = true;
                    ClearCache();
                }
                catch (Exception ex)
                {
                    ExceptionManager.Publish(ex);
                    transaction.RollbackTransaction();
                }
            }
            return ret;
        }
    }
}