<<<<<<< HEAD
/**
 * RentKart Global JavaScript
 * Accessible interactions: Mobile drawer toggle, header shadow on scroll, keyboard traps.
 */

document.addEventListener('DOMContentLoaded', function () {
    initHeaderScroll();
    initMobileNav();
});

/**
 * Adds shadow to sticky header on page scroll
 */
function initHeaderScroll() {
    const header = document.getElementById('mainHeader');
    if (!header) return;

    const handleScroll = () => {
        if (window.scrollY > 10) {
            header.classList.add('rk-header-scrolled');
        } else {
            header.classList.remove('rk-header-scrolled');
        }
    };

    window.addEventListener('scroll', handleScroll, { passive: true });
    handleScroll();
}

/**
 * Handles mobile navigation drawer toggle and keyboard accessibility (Escape to close)
 */
function initMobileNav() {
    const toggleBtn = document.getElementById('mobileMenuToggle');
    const menu = document.getElementById('mobileMenu');
    if (!toggleBtn || !menu) return;

    const toggleMenu = () => {
        const isExpanded = toggleBtn.getAttribute('aria-expanded') === 'true';
        const newState = !isExpanded;

        toggleBtn.setAttribute('aria-expanded', String(newState));
        menu.setAttribute('aria-hidden', String(!newState));
        menu.classList.toggle('is-open', newState);

        if (newState) {
            // Focus first link in drawer
            const firstLink = menu.querySelector('a');
            if (firstLink) firstLink.focus();
        }
    };

    toggleBtn.addEventListener('click', toggleMenu);

    // Escape key closes mobile drawer
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && menu.classList.contains('is-open')) {
            toggleMenu();
            toggleBtn.focus();
        }
    });
}
=======
﻿// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
>>>>>>> a613c19e992d63426af7f61740d6c8fcffdae82f
