using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApplication4.Data;
using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        // VARIABLES TO STORE SESSION VALUES
        const string SessionKeyUserID = "_UserID";
        const string SessionKeyUserEmail = "_UserEmail";

        // CREATE SELECTLIST TO DISPLAY ACCOUNT TYPES IN DROPDOWN LIST
        List<string> AccTypeList = new List<string> { "Business", "Personal" };

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: User
        public IActionResult Index()
        {
            // STORE USERID IN SESSION TO CONTROL VIEWS
            ViewBag.userID = HttpContext.Session.GetString(SessionKeyUserEmail);
            return View();
        }


        // GET: User/Register
        public IActionResult Register()
        {
            // CREATE SELECTLIST TO DISPLAY ACCOUNT TYPES IN DROPDOWN LIST
            //List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, "Business");

            // CHECK IF USER IS ALREADY LOGGED IN
            if (HttpContext.Session.GetString(SessionKeyUserID) == null)
            {
                return View();
            }
            else
            {
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: User/Register
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register([Bind("acc_type,CompanyName,FirstName,LastName,ProfilePicture,BirthDate,Email,Password")] UserModel userModel)
        {
            // CREATE SELECTLIST TO DISPLAY ACCOUNT TYPES IN DROPDOWN LIST WHEN THE VIEW IS REDISPLAYED WITH ERROR
            List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, "Business");

            // FETCH THE SELECTED ACCOUNT TYPE FROM DROPDOWN LIST
            string SelectedAccType = Request.Form["acc_type"].ToString();
            if (SelectedAccType == "Business")
            {
                userModel.FirstName = "";
                userModel.LastName = "";
            }
            else
            {
                userModel.acc_type = "Personal";
                userModel.CompanyName = "";
            }

            // TRY TO REGISTER USER INTO THE DATABASE
            try
            {
                // CHECK EMAIL ADDRESS FOR DUPLICATE RECORD
                var DbEmail = from e in _context.UserModel
                              where e.Email == userModel.Email
                              select e.Email;

                if (userModel.Email == DbEmail.FirstOrDefault())
                {
                    ViewBag.Message = "Email address already exists.";
                    return View();
                }
                else
                {
                    // CHECK FOR USER'S AGE (18 YEARS ~ 6575 DAYS)
                    var Age = AgeSpan(userModel.BirthDate);

                    if (Math.Ceiling(Age) <= 6575)
                    {
                        ViewBag.Message = "You should be 18 years or older to register.";
                        return View();
                    }
                    else
                    {
                        // SET THE SESSION VALUE AND REGISTER USER
                        HttpContext.Session.SetString(SessionKeyUserEmail, userModel.Email);
                        _context.Add(userModel);
                        await _context.SaveChangesAsync();

                        var DbUserRec = _context.UserModel.Where(u => u.Email == userModel.Email && u.Password == userModel.Password).FirstOrDefault();
                        HttpContext.Session.SetString(SessionKeyUserID, DbUserRec.ID.ToString());
                        return RedirectToAction(nameof(Index));
                    }

                }
            }
            catch (Exception)
            {
                ViewBag.Message = "Something went wrong.";
                return View();

            }
        }


        public IActionResult Login()
        {
            // CHECK IF USER IS ALREADY LOGGED IN
            if (HttpContext.Session.GetString(SessionKeyUserID) != null)
            {
                return RedirectToAction(nameof(Index));
            }
            return View();
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Login(UserModel userModel)
        {
            // COMPARE EMAIL AND PASSWORD WITH DATABASE VALUES RESPECTING THE CASE
            var login = _context.UserModel
                .Where(u => string.Equals(u.Email, userModel.Email, StringComparison.CurrentCultureIgnoreCase) && string.Equals(u.Password, userModel.Password, StringComparison.CurrentCulture))
                .FirstOrDefault();
            if (login != null)
            {
                // IF USER IS REGISTERED, SAVE SESSION VALUES AND LOG IN
                HttpContext.Session.SetString(SessionKeyUserID, login.ID.ToString());
                HttpContext.Session.SetString(SessionKeyUserEmail, userModel.Email);

                // REDIRECT TO APPLICATION HOME PAGE
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelError("", "Username or password incorrect.");
            }

            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }


        public IActionResult ManageProfile()
        {
            // FETCH USER ID FROM SESSION
            var id = HttpContext.Session.GetString(SessionKeyUserID);
            var email = HttpContext.Session.GetString(SessionKeyUserEmail);

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            // FETCH USER INFORMATION FROM DATABASE AND SEND TO THE VIEW
            var userModel = _context.UserModel.SingleOrDefault(m => m.ID == Convert.ToInt32(id));
            if (userModel == null)
            {
                return NotFound();
            }

            //List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, userModel.acc_type);

            // SEND SESSION INFO TO CONTROL THE VIEW
            ViewBag.UserID = email;
            return View(userModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageProfile([Bind("ID, acc_type, CompanyName, FirstName, LastName, ProfilePicture, BirthDate, Email")] UserModel userModel)
        {
            // FETCH USER ID FROM SESSION TO GET THE DATABASE VALUE
            var UserID = HttpContext.Session.GetString(SessionKeyUserID);

            //List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, userModel.acc_type);

            // USER IS NOT ALLOWED TO UPATE PASSWORD,
            // SO FETCH REGISTERED PASSWORD FROM DATABASE,
            // AND STORE IT INTO USERMODEL
            // TO AVOID NULL UPDATION TO PASSWORD FIELD
            var DbPassword = from e in _context.UserModel
                             where e.ID == Convert.ToInt32(UserID)
                             select e.Password;
            userModel.Password = DbPassword.First();

            if (UserID != userModel.ID.ToString())
            {
                return NotFound();
            }

            // TRY UPDATING RECORD
            try
            {
                // GET THE SELECTED ACCOUNT TYPE BY USER
                string SelectedAccType = Request.Form["acc_type"].ToString();
                if (SelectedAccType == "Business")
                {
                    userModel.FirstName = "";
                    userModel.LastName = "";
                }
                else
                {
                    userModel.acc_type = "Personal";
                    userModel.CompanyName = "";
                }

                // FETCH EMAIL OF OTHER USER TO CHECK 
                // IF USER EDITED EMAIL ADDRESS MATCHES 
                // WITH ANY OTHER USER EMAIL ADDRESS
                var DbEmail = from e in _context.UserModel
                              where e.Email == userModel.Email &&
                                            e.ID != Convert.ToInt32(HttpContext.Session.GetString(SessionKeyUserID))
                              select e.Email;

                if (userModel.Email == DbEmail.FirstOrDefault())
                {
                    ViewBag.Message = "Email address already exists.";
                    return View();
                }
                else
                {
                    // CHECK FOR THE USER'S AGE (18 YEARS ~ 6575 DAYS)
                    var Age = AgeSpan(userModel.BirthDate);

                    if (Math.Ceiling(Age) <= 6575)
                    {
                        ViewBag.Message = "You should be 18 years or older to register.";
                        return View();
                    }
                    else
                    {
                        // UPDATE SESSION INFO FOR ANY CHANGES
                        // AND UPDATE THE DATABASE
                        HttpContext.Session.SetString(SessionKeyUserID, userModel.ID.ToString());
                        HttpContext.Session.SetString(SessionKeyUserEmail, userModel.Email);

                        _context.Update(userModel);
                        await _context.SaveChangesAsync();
                        ViewBag.Message = "Record updated.";

                        // SEND SESSION INFO TO CONTROL THE VIEW
                        ViewBag.UserID = HttpContext.Session.GetString(SessionKeyUserEmail);
                        return View(userModel);
                    }
                }
            }
            catch (Exception e)
            {
                //ViewBag.Message = e.ToString();
                ViewBag.Message = "Something went wrong.";
            }
            //return RedirectToAction(nameof(Index));
            return View(userModel);
        }

        // METHOD TO CHECK USER'S AGE, 
        // RETURNS TOTAL NUMBER OF DAYS BETWEEN TODAY AND USER'S BIRTHDAY 
        private double AgeSpan(DateTime birthDate)
        {
            return DateTime.Now.Subtract(birthDate).TotalDays;
        }
    }
}