﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Caching.Memory;
using PartsUnlimited.Models;
using PartsUnlimited.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PartsUnlimited.Cache;

namespace PartsUnlimited.Controllers
{
    public class HomeController : Controller
    {
        private readonly IPartsUnlimitedContext _db;
        private readonly IPartsUnlimitedCache _cache;

        public HomeController(IPartsUnlimitedContext context, IPartsUnlimitedCache cache)
        {
            _db = context;
            _cache = cache;
        }

        //
        // GET: /Home/
        public async Task<IActionResult> Index()
        {
            // Get most popular products
            List<Product> topSellingProducts;
            var topProductResult = await _cache.TryGetValue<List<Product>>("topselling");
            if (!topProductResult.HasValue)
            {
                topSellingProducts = GetTopSellingProducts(4);
                //Refresh it every 10 minutes. Let this be the last item to be removed by cache if cache GC kicks in.
                await _cache.Set("topselling", topSellingProducts, new PartsUnlimitedMemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10)).SetPriority(PartsUnlimitedCacheItemPriority.High));
            }
            else
            {
                topSellingProducts = topProductResult.Value;
            }
            
            List<Product> newProducts;
            var newProductResult = await _cache.TryGetValue<List<Product>>("newarrivals");
            if (!newProductResult.HasValue)
            {
                newProducts = GetNewProducts(4);
                //Refresh it every 10 minutes. Let this be the last item to be removed by cache if cache GC kicks in.
                await _cache.Set(
                    "newarrivals", newProducts,
                    new PartsUnlimitedMemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                        .SetPriority(PartsUnlimitedCacheItemPriority.High));
            }
            else
            {
                newProducts =newProductResult.Value;
            }

            var viewModel = new HomeViewModel
            {
                NewProducts = newProducts,
                TopSellingProducts = topSellingProducts,
                CommunityPosts = GetCommunityPosts()
            };

            return View(viewModel);
        }

        //Can be removed and handled when HandleError filter is implemented
        //https://github.com/aspnet/Mvc/issues/623
        public IActionResult Error()
        {
            return View("~/Views/Shared/Error.cshtml");
        }

        private List<Product> GetTopSellingProducts(int count)
        {
            // Group the order details by product and return
            // the products with the highest count

            // TODO [EF] We don't query related data as yet, so the OrderByDescending isn't doing anything
            return _db.Products
                .OrderByDescending(a => a.OrderDetails.Count())
                .Take(count)
                .ToList();
        }

        private List<Product> GetNewProducts(int count)
        {
            return _db.Products
                .OrderByDescending(a => a.Created)
                .Take(count)
                .ToList();
        }

        private List<CommunityPost> GetCommunityPosts()
        {
            return new List<CommunityPost>{
                new CommunityPost {
                    Content= "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus commodo tellus lorem, et bibendum velit sagittis in. Integer nisl augue, cursus id tellus in, sodales porta.",
                    DatePosted = DateTime.Now,
                    Image = "community_1.png",
                    Source = CommunitySource.Facebook
                },
                new CommunityPost {
                    Content= " Donec tincidunt risus in ligula varius, feugiat placerat nisi condimentum. Quisque rutrum eleifend venenatis. Phasellus a hendrerit urna. Cras arcu leo, hendrerit vel mollis nec.",
                    DatePosted = DateTime.Now,
                    Image = "community_2.png",
                    Source = CommunitySource.Facebook
                },
                new CommunityPost {
                    Content= "Aenean vestibulum non lacus non molestie. Curabitur maximus interdum magna, ullamcorper facilisis tellus fermentum eu. Pellentesque iaculis enim ac vestibulum mollis.",
                    DatePosted = DateTime.Now,
                    Image = "community_3.png",
                    Source = CommunitySource.Facebook
                },
                new CommunityPost {
                    Content= "Ut consectetur sed justo vel convallis. Vestibulum quis metus leo. Nulla hendrerit pharetra dui, vel euismod lectus elementum sit amet. Nam dolor turpis, sodales non mi nec.",
                    DatePosted = DateTime.Now,
                    Image = "community_4.png",
                    Source = CommunitySource.Facebook
                }
            };
        }
    }
}
