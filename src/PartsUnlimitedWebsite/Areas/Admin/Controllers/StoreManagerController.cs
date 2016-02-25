﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.Data.Entity;
using Microsoft.Framework.Caching.Memory;
using PartsUnlimited.Hubs;
using PartsUnlimited.Models;
using PartsUnlimited.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PartsUnlimited.Areas.Admin.Controllers
{
    public enum SortField { Name, Title, Price }
    public enum SortDirection { Up, Down }

    public class StoreManagerController : AdminController
    {
        private readonly IPartsUnlimitedContext _db;
        private readonly IHubContext _annoucementHub;
        private readonly IMemoryCache _cache;

        public StoreManagerController(IPartsUnlimitedContext context, IConnectionManager connectionManager, IMemoryCache memoryCache)
        {
            _db = context;
            _annoucementHub = connectionManager.GetHubContext<AnnouncementHub>();
            _cache = memoryCache;
        }

        //
        // GET: /StoreManager/

        public IActionResult Index(SortField sortField = SortField.Name, SortDirection sortDirection = SortDirection.Up)
        {
            // TODO [EF] Swap to native support for loading related data when available
            var products = from product in _db.Products
                           join category in _db.Categories on product.CategoryId equals category.CategoryId
                           select new Product()
                           {
                               ProductArtUrl = product.ProductArtUrl,
                               ProductId = product.ProductId,
                               CategoryId = product.CategoryId,
                               Price = product.Price,
                               Title = product.Title,
                               Category = new Category()
                               {
                                   CategoryId = product.CategoryId,
                                   Name = category.Name
                               }
                           };

            var sorted = Sort(products, sortField, sortDirection);

            return View(sorted);
        }

        private IQueryable<Product> Sort(IQueryable<Product> products, SortField sortField, SortDirection sortDirection)
        {
            if (sortField == SortField.Name)
            {
                if (sortDirection == SortDirection.Up)
                {
                    return products.OrderBy(o => o.Category.Name);
                }
                else
                {
                    return products.OrderByDescending(o => o.Category.Name);
                }
            }

            if (sortField == SortField.Price)
            {
                if (sortDirection == SortDirection.Up)
                {
                    return products.OrderBy(o => o.Price);
                }
                else
                {
                    return products.OrderByDescending(o => o.Price);
                }
            }

            if (sortField == SortField.Title)
            {
                if (sortDirection == SortDirection.Up)
                {
                    return products.OrderBy(o => o.Title);
                }
                else
                {
                    return products.OrderByDescending(o => o.Title);
                }
            }

            // Should not reach here, but return products for compiler
            return products;
        }


        //
        // GET: /StoreManager/Details/5

        public IActionResult Details(int id)
        {
            string cacheId = string.Format("product_{0}", id);

            Product product;
            if (!_cache.TryGetValue(cacheId, out product))
            {
                //If this returns null, don't stick it in the cache
                product =  _db.Products.Where(a => a.ProductId == id).FirstOrDefault();

                if (product != null)
                {
                    //                               Remove it from cache if not retrieved in last 10 minutes
                    _cache.Set(cacheId, product, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10)));
                }
            }

            if (product == null)
            {
                _cache.Remove(cacheId);
                return View(product);
            }

            // TODO [EF] We don't query related data as yet. We have to populate this until we do automatically.
            product.Category = _db.Categories.Single(g => g.CategoryId == product.CategoryId);
            return View(product);
        }

        //
        // GET: /StoreManager/Create
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(_db.Categories, "CategoryId", "Name");
            return View();
        }

        // POST: /StoreManager/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (ModelState.IsValid)
            {
                _db.Products.Add(product);
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
                _annoucementHub.Clients.All.announcement(new ProductData() { Title = product.Title, Url = Url.Action("Details", "Store", new { id = product.ProductId }) });
                _cache.Remove("announcementProduct");
                return RedirectToAction("Index");
            }

            ViewBag.Categories = new SelectList(_db.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        //
        // GET: /StoreManager/Edit/5
        public IActionResult Edit(int id)
        {
            Product product = _db.Products.Where(a => a.ProductId == id).FirstOrDefault();
            ViewBag.Categories = new SelectList(_db.Categories, "CategoryId", "Name", product.CategoryId).ToList();

            if (product == null)
            {
                return View(product);
            }

            return View(product);
        }

        //
        // POST: /StoreManager/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product)
        {
            if (ModelState.IsValid)
            {
                _db.Entry(product).State = EntityState.Modified;
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
                //Invalidate the cache entry as it is modified
                _cache.Remove(string.Format("product_{0}", product.ProductId));
                return RedirectToAction("Index");
            }

            ViewBag.Categories = new SelectList(_db.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        //
        // GET: /StoreManager/RemoveProduct/5
        public IActionResult RemoveProduct(int id)
        {
            Product product = _db.Products.Where(a => a.ProductId == id).FirstOrDefault();
            return View(product);
        }

        //
        // POST: /StoreManager/RemoveProduct/5
        [HttpPost, ActionName("RemoveProduct")]
        public async Task<IActionResult> RemoveProductConfirmed(int id)
        {
            Product product = _db.Products.Where(a => a.ProductId == id).FirstOrDefault();
            CartItem cartItem = _db.CartItems.Where(a => a.ProductId == id).FirstOrDefault();
            List<OrderDetail> orderDetail = _db.OrderDetails.Where(a => a.ProductId == id).ToList();
            List<Raincheck> rainCheck = _db.RainChecks.Where(a => a.ProductId == id).ToList();

            if (product != null)
            {
                if (cartItem != null)
                {
                    _db.CartItems.Remove(cartItem);
                    await _db.SaveChangesAsync(HttpContext.RequestAborted);
                }

                if (orderDetail != null)
                {
                    _db.OrderDetails.RemoveRange(orderDetail);
                    await _db.SaveChangesAsync(HttpContext.RequestAborted);
                }

                if (rainCheck != null)
                {
                    _db.RainChecks.RemoveRange(rainCheck);
                    await _db.SaveChangesAsync(HttpContext.RequestAborted);
                }

                _db.Products.Remove(product);
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
                //Remove the cache entry as it is removed
                _cache.Remove(string.Format("product_{0}", id));
            }

            return RedirectToAction("Index");
        }

#if TESTING
        //
        // GET: /StoreManager/GetProductIdFromName
        // Note: Added for automated testing purpose. Application does not use this.
        [HttpGet]
        public IActionResult GetProductIdFromName(string productName)
        {
            var product = db.Products.Where(a => a.Title == productName).FirstOrDefault();

            if (product == null)
            {
                return HttpNotFound();
            }

            return new ContentResult { Content = product.ProductId.ToString(), ContentType = "text/plain" };
        }
#endif
    }
}
