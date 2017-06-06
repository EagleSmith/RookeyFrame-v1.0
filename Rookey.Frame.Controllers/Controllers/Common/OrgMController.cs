/*----------------------------------------------------------------
        // Copyright (C) Rookey
        // 版权所有
        // 开发者：rookey
        // Email：rookey@yeah.net
        // 
//----------------------------------------------------------------*/

using System;
using System.Web;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Linq;
using Rookey.Frame.Common;
using Rookey.Frame.Model.OrgM;
using Rookey.Frame.Operate.Base;
using System.Threading.Tasks;
using Rookey.Frame.Controllers.Attr;
using Rookey.Frame.Controllers.Other;
using Rookey.Frame.Base;
using Rookey.Frame.Operate.Base.OperateHandle;

namespace Rookey.Frame.Controllers.OrgM
{
    /// <summary>
    /// 组织机构相关操作控制器（异步）
    /// </summary>
    public class OrgMAsyncController : AsyncBaseController
    {
        /// <summary>
        /// 异步获取部门职务
        /// </summary>
        /// <returns></returns>
        [OpTimeMonitor]
        public Task<ActionResult> GetDeptDutysAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                return new OrgMController(Request).GetDeptDutys();
            }).ContinueWith<ActionResult>(task =>
            {
                return task.Result;
            });
        }

        /// <summary>
        /// 异步获取员工的层级部门信息
        /// </summary>
        /// <returns></returns>
        public Task<ActionResult> GetEmpLevelDepthDeptAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                return new OrgMController(Request).GetEmpLevelDepthDept();
            }).ContinueWith<ActionResult>(task =>
            {
                return task.Result;
            });
        }
    }

    /// <summary>
    /// 组织机构相关操作控制器
    /// </summary>
    public class OrgMController : BaseController
    {
        #region 构造函数

        private HttpRequestBase _Request = null; //请求对象

        /// <summary>
        /// 无参构造函数
        /// </summary>
        public OrgMController()
        {
            _Request = Request;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="request">请求对象</param>
        public OrgMController(HttpRequestBase request)
            : base(request)
        {
            _Request = request;
        }

        #endregion

        /// <summary>
        /// 获取部门职务
        /// </summary>
        /// <returns></returns>
        [OpTimeMonitor]
        public ActionResult GetDeptDutys()
        {
            if (_Request == null) _Request = Request;
            SetRequest(_Request);
            Guid deptId = _Request["deptId"].ObjToGuid();
            List<OrgM_Duty> dutys = OrgMOperate.GetDeptDutys(deptId);
            dutys.Insert(0, new OrgM_Duty() { Id = Guid.Empty, Name = "请选择" });
            return Json(dutys, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// 获取员工的层级部门信息
        /// </summary>
        /// <returns></returns>
        public ActionResult GetEmpLevelDepthDept()
        {
            if (_Request == null) _Request = Request;
            SetRequest(_Request);
            int levelDepth = _Request["levelDepth"].ObjToInt(); //层级
            Guid empId = _Request["empId"].ObjToGuid(); //员工ID
            Guid? companyId = _Request["companyId"].ObjToGuidNull(); //所属公司，集团模式下用到
            Guid? deptId = _Request["deptId"].ObjToGuidNull(); //兼职部门，以兼职部门找
            if (empId == Guid.Empty || levelDepth < 0)
                return Json(null);
            //层级部门
            OrgM_Dept depthDept = OrgMOperate.GetEmpLevelDepthDept(levelDepth, empId, companyId, deptId);
            //当前部门
            OrgM_Dept currDept = deptId.HasValue && deptId.Value != Guid.Empty ? OrgMOperate.GetDeptById(deptId.Value) : OrgMOperate.GetEmpMainDept(empId, companyId);
            return Json(new { CurrDept = currDept, DepthDept = depthDept });
        }

        /// <summary>
        /// 上传员工照片，照片路径/Upload/
        /// </summary>
        /// <returns></returns>
        public ActionResult UploadEmpPhoto()
        {
            Guid id = Request["id"].ObjToGuid();
            string filePath = Request["filePath"].ObjToStr();
            string errMsg = string.Empty;
            if (id != Guid.Empty && !string.IsNullOrWhiteSpace(filePath))
            {
                string pathFlag = "\\";
                if (WebConfigHelper.GetAppSettingValue("IsLinux") == "true")
                    pathFlag = "/";
                else
                    filePath = filePath.Replace("/", "\\");
                if (filePath.StartsWith(pathFlag))
                    filePath = filePath.Substring(pathFlag.Length, filePath.Length - pathFlag.Length);
                filePath = Globals.GetWebDir() + filePath;
                string dir = Globals.GetWebDir() + "Upload" + pathFlag + "Image" + pathFlag + "Emp";
                try
                {
                    if (!System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);
                    string newFile = dir + pathFlag + id.ToString() + System.IO.Path.GetExtension(filePath);
                    System.IO.File.Copy(filePath, newFile, true);
                }
                catch (Exception ex)
                {
                    errMsg = ex.Message;
                }
            }
            return Json(new ReturnResult() { Success = string.IsNullOrEmpty(errMsg), Message = errMsg });
        }

        /// <summary>
        /// 添加部门
        /// </summary>
        /// <returns></returns>
        public ActionResult AddDept()
        {
            string deptname = Request["deptname"].ObjToStr();
            if (string.IsNullOrWhiteSpace(deptname))
                return Json(new ReturnResult() { Success = false, Message = "部门名称不能为空" });
            string errMsg = string.Empty;
            long num = CommonOperate.Count<OrgM_Dept>(out errMsg, false, x => x.Name == deptname);
            if (num > 0)
                return Json(new ReturnResult() { Success = false, Message = "该部门已存在，请不要重复添加" });
            UserInfo currUser = GetCurrentUser(Request);
            OrgM_Dept dept = new OrgM_Dept()
            {
                Name = deptname,
                Alias = deptname,
                IsValid = true,
                EffectiveDate = DateTime.Now,
                CreateDate = DateTime.Now,
                CreateUserId = currUser.UserId,
                CreateUserName = currUser.EmpName,
                ModifyDate = DateTime.Now,
                ModifyUserId = currUser.UserId,
                ModifyUserName = currUser.EmpName
            };
            Guid deptId = CommonOperate.OperateRecord<OrgM_Dept>(dept, ModelRecordOperateType.Add, out errMsg, null, false);
            if (deptId != Guid.Empty)
                return Json(new { Success = true, Message = string.Empty, DeptId = deptId, DeptName = deptname });
            else
                return Json(new ReturnResult() { Success = false, Message = errMsg });
        }

        /// <summary>
        /// 添加职务
        /// </summary>
        /// <returns></returns>
        public ActionResult AddDuty()
        {
            Guid deptId = Request["deptId"].ObjToGuid();
            if (deptId == Guid.Empty)
                return Json(new ReturnResult() { Success = false, Message = "请先选择部门" });
            OrgM_Dept dept = OrgMOperate.GetDeptById(deptId);
            if (dept == null)
                return Json(new ReturnResult() { Success = false, Message = "选择的部门不存在" });
            string dutyname = Request["dutyname"].ObjToStr();
            if (string.IsNullOrWhiteSpace(dutyname))
                return Json(new ReturnResult() { Success = false, Message = "职务名称不能为空" });
            string errMsg = string.Empty;
            long num = CommonOperate.Count<OrgM_Dept>(out errMsg, false, x => x.Name == dutyname);
            if (num > 0)
                return Json(new ReturnResult() { Success = false, Message = "该职务已存在，请不要重复添加" });
            UserInfo currUser = GetCurrentUser(Request);
            OrgM_Duty duty = new OrgM_Duty()
            {
                Name = dutyname,
                IsValid = true,
                EffectiveDate = DateTime.Now,
                CreateDate = DateTime.Now,
                CreateUserId = currUser.UserId,
                CreateUserName = currUser.EmpName,
                ModifyDate = DateTime.Now,
                ModifyUserId = currUser.UserId,
                ModifyUserName = currUser.EmpName
            };
            Guid dutyId = CommonOperate.OperateRecord<OrgM_Duty>(duty, ModelRecordOperateType.Add, out errMsg, null, false);
            if (dutyId != Guid.Empty)
            {
                Guid? parentId = null;
                List<OrgM_DeptDuty> positions = OrgMOperate.GetDeptPositions(deptId);
                if (positions.Count > 0)
                {
                    OrgM_DeptDuty leaderPosition = positions.Where(x => x.IsDeptCharge).FirstOrDefault();
                    if (leaderPosition != null)
                        parentId = leaderPosition.Id;
                }
                OrgM_DeptDuty position = new OrgM_DeptDuty()
                {
                    Name = string.Format("{0}-{1}", string.IsNullOrEmpty(dept.Alias) ? dept.Name : dept.Alias, dutyname),
                    OrgM_DeptId = deptId,
                    OrgM_DutyId = dutyId,
                    ParentId = parentId,
                    IsValid = true
                };
                CommonOperate.OperateRecord<OrgM_DeptDuty>(position, ModelRecordOperateType.Add, out errMsg, null, false);
                return Json(new { Success = true, Message = string.Empty, DutyId = dutyId });
            }
            else
            {
                return Json(new ReturnResult() { Success = false, Message = errMsg });
            }
        }
    }
}
